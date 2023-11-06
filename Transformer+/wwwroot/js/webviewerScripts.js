let wvInstance;
window.webviewerFunctions = {
    initWebViewer: function (url) {
        const viewerElement = document.getElementById('viewer');
        WebViewer({
            enableRedaction: true,
            fullAPI: true,
            path: 'lib',
            initialDoc: url || `https://pdftron.s3.amazonaws.com/downloads/pl/demo-annotated.pdf`,
        }, viewerElement).then((instance) => {
            instance.UI.setTheme('dark');
            wvInstance = instance;
        })
    },
    loadDocument: function (url) {
        wvInstance.loadDocument(url);
    },
}