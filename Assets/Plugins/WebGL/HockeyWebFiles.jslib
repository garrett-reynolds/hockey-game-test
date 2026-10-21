// Browser side of CHANGE LOGO and the save flush (#228). Web build only.
mergeInto(LibraryManager.library, {

  // Opens the browser's file box for one PNG or JPG. Calls callback(ptr, length):
  //   length -1: Cancel;  length 0: empty or unreadable file;  otherwise the file bytes at ptr.
  // The bytes are malloc'd here and freed here right after the call, so the C# side must copy them.
  HockeyPickPicture: function (callback) {
    var finished = false;
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/png,image/jpeg';
    input.style.display = 'none';
    document.body.appendChild(input);

    function answer(ptr, length) {
      {{{ makeDynCall('vii', 'callback') }}}(ptr, length);
    }

    function finish(file) {
      if (finished) return;
      finished = true;
      window.removeEventListener('focus', onFocus);
      if (input.parentNode) input.parentNode.removeChild(input);
      if (!file) { answer(0, -1); return; }
      var reader = new FileReader();
      reader.onload = function () {
        var bytes = new Uint8Array(reader.result);
        if (bytes.length === 0) { answer(0, 0); return; }
        var ptr = _malloc(bytes.length);
        try {
          HEAPU8.set(bytes, ptr);
          answer(ptr, bytes.length);
        } finally {
          _free(ptr);
        }
      };
      reader.onerror = function () { answer(0, 0); };
      reader.readAsArrayBuffer(file);
    }

    // Some browsers give no cancel event: when the page gets focus back and no file arrived, treat it as Cancel.
    function onFocus() {
      setTimeout(function () {
        if (!finished && !(input.files && input.files.length)) finish(null);
      }, 1000);
    }

    input.addEventListener('change', function () {
      finish(input.files && input.files.length ? input.files[0] : null);
    });
    input.addEventListener('cancel', function () { finish(null); });
    window.addEventListener('focus', onFocus);
    input.click();
  },

  // Copies Unity's in-memory files (persistentDataPath) to the browser's IndexedDB. callback(1) worked, callback(0) failed.
  HockeyFlushFiles: function (callback) {
    if (typeof FS === 'undefined' || !FS.syncfs) { {{{ makeDynCall('vi', 'callback') }}}(0); return; }
    FS.syncfs(false, function (err) {
      {{{ makeDynCall('vi', 'callback') }}}(err ? 0 : 1);
    });
  }
});
