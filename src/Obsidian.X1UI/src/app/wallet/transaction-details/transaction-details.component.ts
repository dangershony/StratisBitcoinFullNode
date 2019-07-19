import { Component, Input, OnInit, OnDestroy } from '@angular/core';
import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Subscription } from 'rxjs';

import { ApiService } from '@shared/services/api.service';
import { GlobalService } from '@shared/services/global.service';
import { ModalService } from '@shared/services/modal.service';

import { WalletInfo } from '@shared/models/wallet-info';
import { TransactionInfo } from '@shared/models/transaction-info';
import { RequestObject, ResponseWrapper, ResponsePayload } from "@shared/services/visualcrypt-dtos";
import { clearInterval } from 'timers';

@Component({
  selector: 'transaction-details',
  templateUrl: './transaction-details.component.html',
  styleUrls: ['./transaction-details.component.css']
})
export class TransactionDetailsComponent implements OnInit, OnDestroy {

  @Input() transaction: TransactionInfo;
  constructor(private apiService: ApiService, private globalService: GlobalService, private genericModalService: ModalService, public activeModal: NgbActiveModal) {}

  public copied: boolean = false;
  public coinUnit: string;
  public confirmations: number;
  private lastBlockSyncedHeight: number;

  private polling: any;

  ngOnInit() {
    this.getGeneralInfo();
    this.polling = setInterval(this.getGeneralInfo.bind(this), 5000);
    this.coinUnit = this.globalService.getCoinUnit();
  }

  ngOnDestroy() {
    clearInterval(this.polling);
  }

  public onCopiedClick() {
    this.copied = true;
  }

  private getGeneralInfo() {
    const walletInfo = new WalletInfo(this.globalService.getWalletName());
    this.apiService.makeRequest(new RequestObject("generalInfo", walletInfo), this.onGetGeneralInfo.bind(this));
  }

  private onGetGeneralInfo(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      const generalWalletInfoResponse = responsePayload.responsePayload;
      this.lastBlockSyncedHeight = generalWalletInfoResponse.lastBlockSyncedHeight;
      this.getConfirmations(this.transaction);
    }
  }

  private getConfirmations(transaction: TransactionInfo) {
    if (transaction.transactionConfirmedInBlock) {
      this.confirmations = this.lastBlockSyncedHeight - Number(transaction.transactionConfirmedInBlock) + 1;
    } else {
      this.confirmations = 0;
    }
  }
}
