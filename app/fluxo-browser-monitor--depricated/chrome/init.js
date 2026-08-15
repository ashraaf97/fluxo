"use strict";
var fluxo = {
    debug: false,
    log: function (msg) {
        if (this.debug) {
            try { console.log(msg); } catch { }
        }
    }
};