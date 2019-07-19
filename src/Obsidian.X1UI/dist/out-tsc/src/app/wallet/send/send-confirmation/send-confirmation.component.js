"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var global_service_1 = require("@shared/services/global.service");
var coin_notation_pipe_1 = require("@shared/pipes/coin-notation.pipe");
var SendConfirmationComponent = /** @class */ (function () {
    function SendConfirmationComponent(globalService, activeModal) {
        this.globalService = globalService;
        this.activeModal = activeModal;
        this.showDetails = false;
    }
    SendConfirmationComponent.prototype.ngOnInit = function () {
        this.coinUnit = this.globalService.getCoinUnit();
        this.transactionFee = new coin_notation_pipe_1.CoinNotationPipe().transform(this.transactionFee);
        if (this.hasOpReturn) {
            this.opReturnAmount = new coin_notation_pipe_1.CoinNotationPipe().transform(this.opReturnAmount);
            this.transaction.amount = +this.transaction.recipients[0].amount + +this.transactionFee + +this.opReturnAmount;
        }
        else {
            this.transaction.amount = +this.transaction.recipients[0].amount + +this.transactionFee;
        }
    };
    SendConfirmationComponent.prototype.toggleDetails = function () {
        this.showDetails = !this.showDetails;
    };
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", Object)
    ], SendConfirmationComponent.prototype, "transaction", void 0);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", Object)
    ], SendConfirmationComponent.prototype, "transactionFee", void 0);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", Boolean)
    ], SendConfirmationComponent.prototype, "sidechainEnabled", void 0);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", Number)
    ], SendConfirmationComponent.prototype, "opReturnAmount", void 0);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", Boolean)
    ], SendConfirmationComponent.prototype, "hasOpReturn", void 0);
    SendConfirmationComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-send-confirmation',
            templateUrl: './send-confirmation.component.html',
            styleUrls: ['./send-confirmation.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [global_service_1.GlobalService, ng_bootstrap_1.NgbActiveModal])
    ], SendConfirmationComponent);
    return SendConfirmationComponent;
}());
exports.SendConfirmationComponent = SendConfirmationComponent;
//# sourceMappingURL=send-confirmation.component.js.map