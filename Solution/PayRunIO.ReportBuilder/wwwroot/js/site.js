window.reportBuilder = {
    // Client-side twin of XmlSyntaxHighlighter.cs — same token classes (xml-delim, xml-tag,
    // xml-attr, xml-value, xml-comment) so the palette in app.css applies to both.
    highlightXml: function (text) {
        const encode = t => t.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
        const span = (cls, t) => `<span class="${cls}">${encode(t)}</span>`;
        const markup = /(<!--[\s\S]*?-->|<!\[CDATA\[[\s\S]*?\]\]>|<![^>]*>)|<([/?]?)([A-Za-z_][\w:.-]*)((?:"[^"]*"|'[^']*'|[^>"'])*?)([/?]?)>/g;
        const attrToken = /("[^"]*"|'[^']*')|(=)|([\w:.-]+)/g;

        let html = "";
        let index = 0;
        let match;

        while ((match = markup.exec(text)) !== null) {
            html += encode(text.substring(index, match.index));

            if (match[1] !== undefined) {
                html += span("xml-comment", match[1]);
            } else {
                html += span("xml-delim", "<" + match[2]) + span("xml-tag", match[3]);

                const attrs = match[4];
                let attrIndex = 0;
                let token;

                while ((token = attrToken.exec(attrs)) !== null) {
                    html += encode(attrs.substring(attrIndex, token.index));
                    html += token[1] !== undefined ? span("xml-value", token[1])
                        : token[2] !== undefined ? span("xml-delim", token[2])
                        : span("xml-attr", token[3]);
                    attrIndex = token.index + token[0].length;
                }

                html += encode(attrs.substring(attrIndex)) + span("xml-delim", match[5] + ">");
            }

            index = match.index + match[0].length;
        }

        return html + encode(text.substring(index));
    },

    // Wires the query editor overlay: a highlighted <pre> backdrop sits behind the textarea, whose
    // own text is transparent (the .highlight-active class), so colours show while editing stays a
    // plain textarea. The backdrop re-renders on input and tracks the textarea's scroll position.
    initQueryEditor: function (textarea) {
        if (!textarea || textarea._rqlRefresh) {
            return;
        }

        const shell = textarea.parentElement;
        const backdrop = shell.querySelector(".query-editor-backdrop");

        if (!backdrop) {
            return;
        }

        const syncScroll = () => {
            backdrop.scrollTop = textarea.scrollTop;
            backdrop.scrollLeft = textarea.scrollLeft;
        };

        const refresh = () => {
            if (textarea._rqlLastValue !== textarea.value) {
                textarea._rqlLastValue = textarea.value;
                // Trailing newline keeps the backdrop's last line scrollable in step with the textarea.
                backdrop.innerHTML = window.reportBuilder.highlightXml(textarea.value) + "\n";
            }
            syncScroll();
        };

        textarea._rqlRefresh = refresh;
        textarea.addEventListener("input", refresh);
        textarea.addEventListener("scroll", syncScroll);
        shell.classList.add("highlight-active");
        refresh();
    },

    refreshQueryEditor: function (textarea) {
        if (textarea && textarea._rqlRefresh) {
            textarea._rqlRefresh();
        } else {
            window.reportBuilder.initQueryEditor(textarea);
        }
    },

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
