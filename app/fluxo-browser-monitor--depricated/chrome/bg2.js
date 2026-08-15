"use strict";
fluxo.monitoring = {
    //member variable
    lastIcon: '',
    lastPopup: '',
    videoList: [],

    //configurations for network request inspection
    config: {
        blockedHosts: ["update.microsoft.com", "windowsupdate.com", "thwawte.com"],
        videoUrls: [".facebook.com|pagelet", "player.vimeo.com/", "instagram.com/p/"],
        fileExts: ["3GP", "7Z", "AVI", "BZ2", "DEB", "DOC", "DOCX", "EXE", "GZ", "ISO",
            "MSI", "PDF", "PPT", "PPTX", "RAR", "RPM", "XLS", "XLSX", "SIT", "SITX", "TAR", "JAR", "ZIP", "XZ"],
        vidExts: ["MP4", "M3U8", "F4M", "WEBM", "OGG", "MP3", "AAC", "FLV", "MKV", "DIVX",
            "MOV", "MPG", "MPEG", "OPUS"],
        blockedMimeList: ["text/javascript", "application/javascript", "text/css", "text/html"],
        mimeList: [],
        videoUrlsWithPostReq: ["ubei/v1/player?key=", "ubei/v1/next?key="]
    },

    //extension state
    state: {
        isFluxoUp: true,
        monitoring: true,
        disabled: false
    },

    run: function () {
        fluxo.messaging.connectWithApp(
            this.onSync.bind(this),
            this.onDisconnet.bind(this));
        fluxo.requestWatcher.attach({
            isMatchingDownload: this.isMatchingDownload.bind(this),
            onDownload: this.onDownload.bind(this),
            onResponse: this.onResponse.bind(this)
        });
        this.setupMenuAndHotkey();
    },

    onSync: function (data) {
        this.state.monitoring = data.enabled;
        this.state.isFluxoUp = true;
        this.config.blockedHosts = data.blockedHosts;
        this.config.videoUrls = data.videoUrls;
        this.config.fileExts = data.fileExts;
        this.config.vidExts = data.vidExts;
        this.videoList = data.vidList;
        if (data.mimeList) {
            this.config.mimeList = data.mimeList;
        }
        if (data.blockedMimeList) {
            this.config.blockedMimeList = data.blockedMimeList;
        }
        this.updateBrowserAction();
    },

    onDisconnet: function () {
        this.state.isFluxoUp = false;
        this.updateBrowserAction();
    },

    isMatchingDownload: function (download, response) {
        if (fluxo.util.isBlocked(download.finalUrl||download.url) || download.method === "POST") {
            fluxo.log("blocked: " + (download.finalUrl||download.url));
            return false;
        }
        if (download.filename && fluxo.util.hasMatchingExtension(download.filename)) {
            return true;
        }
        if (fluxo.util.hasMatchingUrlOrAttachment(response)) {
            return true;
        }
        fluxo.log("skip as extension is not maching: " + download.filename);
        return false;
    },

    onDownload: function (download, request, response) {
        var filename = download.filename;
        if (!filename) {
            filename = fluxo.util.guessFileName(response);
        }
        fluxo.messaging.sendToFluxo(request, response, filename, false, download.referrer);
    },

    onResponse: function (request, response) {
        if (this.isMonitoring()) {
            this.detectVideoStream(request, response);
        }
    },

    detectVideoStream: function (request, response) {
        if (!request) return;
        if (fluxo.util.isStreamingVideo(response)) {
            if (request.tabId != -1) {
                chrome.tabs.get(
                    request.tabId,
                    function (tab) {
                        fluxo.messaging.sendToFluxo(request, response, tab.title, true, tab.url);
                    }
                );
                return;
            }
            fluxo.messaging.sendToFluxo(request, response, fluxo.util.guessFileName(response), true);
        }
    },

    isMonitoring: function () {
        return this.state.isFluxoUp === true &&
            fluxo.monitoring.state.monitoring === true &&
            fluxo.monitoring.state.disabled === false;
    },

    updateBrowserAction: function () {
        if (!fluxo.monitoring.state.isFluxoUp) {
            fluxo.monitoring.setBrowserActionPopUp("fatal.html");
            fluxo.monitoring.setBrowserActionIcon("icon_blocked.png");
            return;
        }
        fluxo.monitoring.setBrowserActionPopUp(fluxo.monitoring.state.monitoring ?
            "status.html" : "disabled.html");
        fluxo.monitoring.setBrowserActionIcon(fluxo.monitoring.state.monitoring &&
            !fluxo.monitoring.state.disabled ? "icon.png" : "icon_disabled.png");

        if (fluxo.monitoring.videoList && fluxo.monitoring.videoList.length > 0) {
            chrome.browserAction.setBadgeText({ text: fluxo.monitoring.videoList.length + "" });
        } else {
            chrome.browserAction.setBadgeText({ text: "" });
        }
    },

    setBrowserActionIcon: function (icon) {
        if (fluxo.monitoring.lastIcon == icon) {
            return;
        }
        chrome.browserAction.setIcon({ path: icon });
        fluxo.monitoring.lastIcon = icon;
    },

    setBrowserActionPopUp: function (pop) {
        if (fluxo.monitoring.lastPopup == pop) {
            return;
        }
        chrome.browserAction.setPopup({ popup: pop });
        fluxo.monitoring.lastPopup = pop;
    },

    runContentScript: function (info, tab) {
        log("running content script");
        chrome.tabs.executeScript({
            file: 'contentscript.js'
        });
    },

    setupMenuAndHotkey: function () {
        chrome.commands.onCommand.addListener(function (command) {
            if (fluxo.monitoring.state.isFluxoUp && fluxo.monitoring.state.monitoring) {
                fluxo.monitoring.state.disabled = !fluxo.monitoring.state.disabled;
            }
        });

        chrome.contextMenus.create({
            title: "Download with Fluxo",
            contexts: ["link", "video", "audio"],
            onclick: this.sendLinkToFluxo.bind(this),
        });

        chrome.contextMenus.create({
            title: "Download Image with Fluxo",
            contexts: ["image"],
            onclick: this.sendImageToFluxo.bind(this),
        });

        chrome.contextMenus.create({
            title: "Download all links",
            contexts: ["all"],
            onclick: this.runContentScript,
        });
    },

    sendImageToFluxo: function (info, tab) {
        if (info.mediaType && "image" == info.mediaType && info.srcUrl) {
            url = info.srcUrl;
        }
        if (!url) {
            url = info.linkUrl;
        }
        if (!url) {
            url = info.pageUrl;
        }
        if (!url) {
            return;
        }
        fluxo.messaging.sendUrlToFluxo(url);
    },

    sendLinkToFluxo: function (info, tab) {
        var url = info.linkUrl;
        if (!url && info.mediaType && ("video" == info.mediaType || "audio" == info.mediaType) && info.srcUrl) {
            url = info.srcUrl;
        }
        if (!url) {
            url = info.pageUrl;
        }
        if (!url) {
            return;
        }
        fluxo.messaging.sendUrlToFluxo(url);
    },

    runContentScript: function (info, tab) {
        fluxo.log("running content script");
        chrome.tabs.executeScript({
            file: 'contentscript.js'
        });
    }
};

fluxo.debug = true;
fluxo.monitoring.run();