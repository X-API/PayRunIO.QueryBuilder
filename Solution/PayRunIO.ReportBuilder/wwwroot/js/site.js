window.reportBuilder = {
    downloadFile: function (fileName, contentType, content) {
        const blob = new Blob([content], { type: contentType });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        URL.revokeObjectURL(url);
    },

    scrollToBottom: function (element) {
        if (element) {
            element.scrollTop = element.scrollHeight;
        }
    },

    loadDraft: function (key) {
        try {
            return window.localStorage.getItem(key);
        } catch (e) {
            return null;
        }
    },

    saveDraft: function (key, value) {
        try {
            window.localStorage.setItem(key, value);
        } catch (e) {
            // Storage unavailable or over quota — persistence is best-effort; ignore.
        }
    },

    clearDraft: function (key) {
        try {
            window.localStorage.removeItem(key);
        } catch (e) {
            // Ignore — nothing to recover if removal fails.
        }
    }
};
