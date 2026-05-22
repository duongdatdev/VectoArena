using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reown.Core.Common.Logging;
using Reown.Core.Common.Model.Errors;
using Reown.AppKit.Unity.Model.Errors;
using Reown.Core.Common.Utils;
using Reown.Sign.Models;
using Reown.Sign.Models.Engine;
using Reown.Sign.Models.Engine.Methods;
using Reown.Sign.Nethereum.Model;
using Reown.Sign.Unity;
using UnityEngine;

namespace Reown.AppKit.Unity
{
    public class WalletConnectConnector : Connector
    {
        public override Account Account
        {
            get
            {
                try
                {
                    return SignClient.AddressProvider.CurrentAccount();
                }
                catch (Exception)
                {
                    return default;
                }
            }
        }

        public override IEnumerable<Account> Accounts
        {
            get
            {
                try
                {
                    return _signClient.AddressProvider.AllAccounts();
                }
                catch (Exception)
                {
                    return Array.Empty<Account>();
                }
            }
        }

        public SignClientUnity SignClient
        {
            get => _signClient;
        }

        public WalletConnectConnector()
        {
            ImageId = "ef1a1fcf-7fe8-4d69-bd6d-fda1345b4400";
            Type = ConnectorType.WalletConnect;
        }

        private ConnectionProposal _connectionProposal;
        private SignClientUnity _signClient;

        protected override Task InitializeAsyncCore(AppKitConfig config, SignClientUnity signClient)
        {
            _signClient = signClient;
            DappSupportedChains = config.supportedChains;

            _signClient.SubscribeToSessionEvent("chainChanged", ActiveChainIdChangedHandler);

            _signClient.SessionUpdatedUnity += ActiveSessionChangedHandler;
            _signClient.SessionDisconnectedUnity += SessionDeletedHandler;

            return Task.CompletedTask;
        }

        private void ActiveSessionChangedHandler(object sender, Session session)
        {
            if (session == null || IsAccountConnected)
                return;

            var account = Account;
            if (string.IsNullOrWhiteSpace(account.Address))
                return;

            OnAccountChanged(new AccountChangedEventArgs(account));
        }

        private async void ActiveChainIdChangedHandler(object sender, SessionEvent<JToken> sessionEvent)
        {
            if (!IsAccountConnected)
                return;

            if (sessionEvent.ChainId == "eip155:0")
                return;

            // Wait for the session to be updated before changing the default chain id
            await Task.Delay(TimeSpan.FromSeconds(1));

            await _signClient.AddressProvider.SetDefaultChainIdAsync(sessionEvent.ChainId);

            OnChainChanged(new ChainChangedEventArgs(sessionEvent.ChainId));
            OnAccountChanged(new AccountChangedEventArgs(Account));
        }

        private void SessionDeletedHandler(object sender, EventArgs e)
        {
            if (!IsAccountConnected)
                return;

            IsAccountConnected = false;
            OnAccountDisconnected(AccountDisconnectedEventArgs.Empty);
        }

        protected override async Task<bool> TryResumeSessionAsyncCore()
        {
            var isResumed = await _signClient.TryResumeSessionAsync();

            if (isResumed && AppKit.SiweController.IsEnabled)
            {
                var siweSessionJson = PlayerPrefs.GetString(SiweController.SessionPlayerPrefsKey);

                // If no siwe session is found, request signature
                if (string.IsNullOrWhiteSpace(siweSessionJson))
                {
                    Debug.Log("[WalletConnectConnector] No Siwe session found. Requesting signature.");
                    OnSignatureRequested();
                    return true;
                }

                var siweSession = JsonConvert.DeserializeObject<SiweSession>(siweSessionJson);

                var addressesMatch = string.Equals(siweSession.EthAddress, Account.Address, StringComparison.InvariantCultureIgnoreCase);
                var chainsMatch = siweSession.EthChainIds.Contains(Core.Utils.ExtractChainReference(Account.ChainId));

                // If siwe session found, but it doesn't match the sign session, request signature (i.e. new siwe session)
                if (!addressesMatch || !chainsMatch)
                    OnSignatureRequested();

                return true;
            }

            return isResumed;
        }

        protected override ConnectionProposal ConnectCore()
        {
            var connectOptions = new ConnectOptions
            {
                OptionalNamespaces = NamespaceFactory.BuildProposedNamespaces(AppKit.NetworkController.ActiveChain, DappSupportedChains)
            };

            _connectionProposal = new WalletConnectConnectionProposal(this, _signClient, connectOptions, AppKit.SiweController);
            return _connectionProposal;
        }

        protected override async Task DisconnectAsyncCore()
        {
            try
            {
                await _signClient.Disconnect();
            }
            catch (Exception)
            {
                AppKit.EventsController.SendEvent(new Event
                {
                    name = "DISCONNECT_ERROR"
                });
                throw;
            }
        }

        protected override async Task ChangeActiveChainAsyncCore(Chain chain)
        {
            if (chain.ChainNamespace == ChainConstants.Namespaces.Evm &&
                ActiveSessionSupportsMethod("wallet_addEthereumChain") &&
                ActiveSessionSupportsMethod("wallet_switchEthereumChain"))
            {
                // MetaMask needs an explicit switch request even when the chain is already part of the WalletConnect session.
                await ChangeActiveMetaMaskChainAsync(chain);
            }
            else
            {
                if (!ActiveSessionIncludesChain(chain.ChainId))
                    throw new ReownNetworkException("Chain is not supported", ErrorType.DISAPPROVED_CHAINS);

                await _signClient.AddressProvider.SetDefaultChainIdAsync(chain.ChainId);
                OnChainChanged(new ChainChangedEventArgs(chain.ChainId));
                OnAccountChanged(new AccountChangedEventArgs(Account));
            }
        }

        private async Task ChangeActiveMetaMaskChainAsync(Chain chain)
        {
            try
            {
                try
                {
                    await AppKit.Evm.RpcRequestAsync<string>("wallet_switchEthereumChain", new SwitchEthereumChain(chain.ChainReference));
                }
                catch (ReownNetworkException e) when (IsMetaMaskUnrecognizedChainError(e))
                {
                    await AppKit.Evm.RpcRequestAsync<string>("wallet_addEthereumChain", ToEthereumChain(chain));
                }

                await _signClient.AddressProvider.SetDefaultChainIdAsync(chain.ChainId);

                await WaitForSessionUpdateAsync(TimeSpan.FromSeconds(5));

                OnChainChanged(new ChainChangedEventArgs(chain.ChainId));
                OnAccountChanged(new AccountChangedEventArgs(Account));
            }
            catch (ReownNetworkException e)
            {
                LogMetaMaskError(e);

                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void LogMetaMaskError(Exception exception)
        {
            try
            {
                var error = JObject.Parse(exception.Message);
                var message = error["message"]?.Value<string>() ??
                    error["error"]?["message"]?.Value<string>() ??
                    exception.Message;
                ReownLogger.LogError($"[MetaMask Error] {message}");
            }
            catch (JsonException)
            {
                ReownLogger.LogError($"[MetaMask Error] {exception.Message}");
            }
        }

        private static bool IsMetaMaskUnrecognizedChainError(Exception exception)
        {
            var message = exception.Message;
            if (string.IsNullOrWhiteSpace(message))
                return false;

            try
            {
                var error = JObject.Parse(message);
                return ContainsErrorCode(error, 4902);
            }
            catch (JsonException)
            {
                return message.Contains("4902");
            }
        }

        private static bool ContainsErrorCode(JToken token, int expectedCode)
        {
            if (token is JObject error && TryReadErrorCode(error, out var code) && code == expectedCode)
                return true;

            if (!token.HasValues)
                return false;

            foreach (var child in token.Children())
            {
                if (ContainsErrorCode(child, expectedCode))
                    return true;
            }

            return false;
        }

        private static bool TryReadErrorCode(JObject error, out int code)
        {
            code = 0;
            var token = error["code"];
            if (token == null)
                return false;

            if (token.Type == JTokenType.Integer)
            {
                code = token.Value<int>();
                return true;
            }

            return token.Type == JTokenType.String && int.TryParse(token.Value<string>(), out code);
        }

        private static EthereumChain ToEthereumChain(Chain chain)
        {
            return new EthereumChain(
                chain.ChainReference,
                chain.Name,
                new Reown.Sign.Nethereum.Model.Currency(
                    chain.NativeCurrency.name,
                    chain.NativeCurrency.symbol,
                    chain.NativeCurrency.decimals
                ),
                new[] { chain.RpcUrl },
                new[] { chain.BlockExplorer.url }
            );
        }

        private async Task WaitForSessionUpdateAsync(TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<bool>();
            var sessionUpdateHandler = new EventHandler<Session>((_, _) => tcs.TrySetResult(true));

            _signClient.SessionUpdatedUnity += sessionUpdateHandler;
            try
            {
                await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            }
            finally
            {
                _signClient.SessionUpdatedUnity -= sessionUpdateHandler;
            }
        }

        protected override Task<Account> GetAccountAsyncCore()
        {
            return Task.FromResult(Account);
        }

        protected override Task<Account[]> GetAccountsAsyncCore()
        {
            return Task.FromResult(Accounts.ToArray());
        }

        private bool ActiveSessionSupportsMethod(string method)
        {
            var @namespace = _signClient.AddressProvider.DefaultNamespace;
            var activeSession = _signClient.AddressProvider.DefaultSession;
            return activeSession.Namespaces[@namespace].Methods.Contains(method);
        }

        private bool ActiveSessionIncludesChain(string chainId)
        {
            var @namespace = _signClient.AddressProvider.DefaultNamespace;
            var activeSession = _signClient.AddressProvider.DefaultSession;
            var activeNamespace = activeSession.Namespaces[@namespace];

            var chainsOk = activeNamespace.TryGetChains(out var approvedChains);
            return chainsOk && approvedChains.Contains(chainId);
        }
    }
}
