"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var api_service_1 = require("@shared/services/api.service");
var modal_service_1 = require("@shared/services/modal.service");
var global_service_1 = require("@shared/services/global.service");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var timers_1 = require("timers");
var AboutComponent = /** @class */ (function () {
    function AboutComponent(globalService, apiService, genericModalService) {
        this.globalService = globalService;
        this.apiService = apiService;
        this.genericModalService = genericModalService;
    }
    AboutComponent.prototype.ngOnInit = function () {
        this.applicationVersion = this.globalService.getApplicationVersion();
        this.polling = setInterval(this.getNodeStatus.bind(this), 5000);
        this.getNodeStatus();
    };
    AboutComponent.prototype.ngOnDestroy = function () {
        timers_1.clearInterval(this.polling);
    };
    AboutComponent.prototype.getNodeStatus = function () {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("nodeStatus", ""), this.onGetNodeStatus.bind(this));
    };
    AboutComponent.prototype.onGetNodeStatus = function (responsePayload) {
        if (responsePayload.status === 200) {
            var nodeStatus = responsePayload.responsePayload;
            this.clientName = nodeStatus.agent;
            this.fullNodeVersion = nodeStatus.version;
            this.network = nodeStatus.network;
            this.protocolVersion = nodeStatus.protocolVersion;
            this.blockHeight = nodeStatus.blockStoreHeight;
            this.dataDirectory = nodeStatus.dataDirectoryPath;
        }
    };
    AboutComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-about',
            templateUrl: './about.component.html',
            styleUrls: ['./about.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [global_service_1.GlobalService, api_service_1.ApiService, modal_service_1.ModalService])
    ], AboutComponent);
    return AboutComponent;
}());
exports.AboutComponent = AboutComponent;
//# sourceMappingURL=about.component.js.map