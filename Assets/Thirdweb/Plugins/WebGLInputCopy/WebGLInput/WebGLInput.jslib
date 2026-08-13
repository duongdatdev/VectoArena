var WebGLInput = {
    $instances: [],
    WebGLInputInit : function() {
    },
    WebGLInputCreate: function (canvasId, x, y, width, height, fontsize, text, placeholder, isMultiLine, isPassword, isHidden, isMobile) {

        var container = document.getElementById(UTF8ToString(canvasId));
        var canvas = container ? container.getElementsByTagName('canvas')[0] : null;

        if (!canvas) {
            canvas = document.querySelector('canvas');
        }
        if (!container && canvas) {
            container = canvas.parentNode;
        }
        if (!container || !canvas) {
            console.error('WebGLInput: Unity canvas container was not found.');
            return -1;
        }

        // UI Toolkit coordinates are expressed in Unity render-target pixels.
        // Convert them to CSS pixels using the canvas itself. The surrounding
        // container may include Unity's footer, so its offset size is not valid
        // for input placement.
        var canvasRect = canvas.getBoundingClientRect();
        var containerRect = container.getBoundingClientRect();
        var scaleX = canvas.width ? canvasRect.width / canvas.width : 1;
        var scaleY = canvas.height ? canvasRect.height / canvas.height : 1;

        x = canvasRect.left - containerRect.left + x * scaleX;
        y = canvasRect.top - containerRect.top + y * scaleY;
        width *= scaleX;
        height *= scaleY;
        fontsize *= scaleY;

        var input = document.createElement(isMultiLine?"textarea":"input");
        input.style.position = "absolute";

        if(isMobile) {
            input.style.bottom = 1 + "vh";
            input.style.left = 5 + "vw";
            input.style.width = 90 + "vw";
            input.style.height = (isMultiLine? 18 : 10) + "vh";
            input.style.fontSize = 5 + "vh";
            input.style.borderWidth = 5 + "px";
            input.style.borderColor = "#000000";
        } else {
            input.style.top = y + "px";
            input.style.left = x + "px";
            input.style.width = width + "px";
            input.style.height = height + "px";
            input.style.fontSize = fontsize + "px";
        }

        input.style.outlineWidth = 1 + 'px';
        input.style.opacity = isHidden?0:1;
        input.style.resize = 'none'; // for textarea
        input.style.padding = '0px 1px';
        input.style.cursor = "text";
        input.style.touchAction = 'none';

        // UI Toolkit already draws the field text. On desktop, keep the native
        // browser input transparent but visible so IME works and its caret is shown.
        if (!isMobile && !isHidden) {
            input.style.background = 'transparent';
            input.style.border = 'none';
            input.style.outline = 'none';
            input.style.color = 'transparent';
            input.style.caretColor = '#ffffff';
            input.style.boxSizing = 'border-box';
            input.style.zIndex = '10';
        }

        input.spellcheck = false;
        input.autocomplete = 'off';
        input.autocapitalize = 'none';
        input.value = UTF8ToString(text);
        input.placeholder = UTF8ToString(placeholder);
        input.style.outlineColor = 'black';
        
        if(isPassword){
            input.type = 'password';
        }

        if(isMobile) {
            document.body.appendChild(input);
        } else {
            container.appendChild(input);
        }
        return instances.push(input) - 1;
    },
    WebGLInputEnterSubmit: function(id, falg){
        var input = instances[id];
        // for enter key
        input.addEventListener('keydown', function(e) {
            if ((e.which && e.which === 13) || (e.keyCode && e.keyCode === 13)) {
                if(falg)
                {
                    e.preventDefault();
                    input.blur();
                }
            }
        });
    },
    WebGLInputTab:function(id, cb) {
        var input = instances[id];
        // for tab key
        input.addEventListener('keydown', function (e) {
            if ((e.which && e.which === 9) || (e.keyCode && e.keyCode === 9)) {
                e.preventDefault();

                // if enable tab text
                if(input.enableTabText){
                    var val = input.value;
                    var start = input.selectionStart;
                    var end = input.selectionEnd;
                    input.value = val.substr(0, start) + '\t' + val.substr(end, val.length);
                    input.setSelectionRange(start + 1, start + 1);
                    input.oninput();	// call oninput to exe ValueChange function!!
                } else {
                    {{{ makeDynCall("vii", "cb") }}}(id, e.shiftKey ? -1 : 1);
                }
            }
        });
    },
    WebGLInputFocus: function(id){
        var input = instances[id];
        input.focus();
    },
    WebGLInputOnFocus: function (id, cb) {
        var input = instances[id];
        input.onfocus = function () {
            {{{ makeDynCall("vi", "cb") }}}(id);
        };
    },
    WebGLInputOnBlur: function (id, cb) {
        var input = instances[id];
        input.onblur = function () {
            {{{ makeDynCall("vi", "cb") }}}(id);
        };
    },
    WebGLInputIsFocus: function (id) {
        return instances[id] === document.activeElement;
    },
    WebGLInputOnValueChange:function(id, cb){
        var input = instances[id];
        input.oninput = function () {
            var returnStr = input.value;
            var bufferSize = lengthBytesUTF8(returnStr) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(returnStr, buffer, bufferSize);
            {{{ makeDynCall("vii", "cb") }}}(id, buffer);
        };
    },
    WebGLInputOnEditEnd:function(id, cb){
        var input = instances[id];
        input.onchange = function () {
            var returnStr = input.value;
            var bufferSize = lengthBytesUTF8(returnStr) + 1;
            var buffer = _malloc(bufferSize);
            stringToUTF8(returnStr, buffer, bufferSize);
            {{{ makeDynCall("vii", "cb") }}}(id, buffer);
        };
    },
    WebGLInputOnKeyboardEvent:function(id, cb){
        var input = instances[id];
        var func = function(mode, e) {
            if (e instanceof KeyboardEvent){
                var bufferSize = lengthBytesUTF8(e.key) + 1;
                var key = _malloc(bufferSize);
                stringToUTF8(e.key, key, bufferSize);
                var code = e.code;
                var shift = e.shiftKey ? 1 : 0;
                var ctrl = e.ctrlKey ? 1 : 0;
                var alt = e.altKey ? 1 : 0;
                {{{ makeDynCall("viiiiiii", "cb") }}}(id, mode, key, code, shift, ctrl, alt);
            }
        }
        input.addEventListener('keydown', function(e) { func(1, e); });
        input.addEventListener('keyup', function(e) { func(2, e); });
    },
    WebGLInputSelectionStart:function(id){
        var input = instances[id];
        return input.selectionStart;
    },
    WebGLInputSelectionEnd:function(id){
        var input = instances[id];
        return input.selectionEnd;
    },
    WebGLInputSelectionDirection:function(id){
        var input = instances[id];
        return (input.selectionDirection == "backward")?-1:1;
    },
    WebGLInputSetSelectionRange:function(id, start, end){
        var input = instances[id];
        input.setSelectionRange(start, end);
    },
    WebGLInputMaxLength:function(id, maxlength){
        var input = instances[id];
        input.maxLength = maxlength;
    },
    WebGLInputText:function(id, text){
        var input = instances[id];
        input.value = UTF8ToString(text);
    },
    WebGLInputDelete:function(id){
        var input = instances[id];
        if (!input) {
            return;
        }

        // Removing a focused HTML input fires blur synchronously. Detach the
        // managed callbacks first so scene teardown cannot re-enter IL2CPP
        // through a callback whose Unity object is already being destroyed.
        input.onfocus = null;
        input.onblur = null;
        input.oninput = null;
        input.onchange = null;

        if (input.parentNode) {
            input.parentNode.removeChild(input);
        }
        instances[id] = null;
    },
    WebGLInputEnableTabText:function(id, enable) {
        var input = instances[id];
        input.enableTabText = enable;
    },
    WebGLInputForceBlur:function(id) {
        var input = instances[id];
        input.blur();
    },
}

autoAddDeps(WebGLInput, '$instances');
mergeInto(LibraryManager.library, WebGLInput);
