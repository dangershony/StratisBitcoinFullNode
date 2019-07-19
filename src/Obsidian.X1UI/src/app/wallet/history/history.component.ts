import { Component, OnInit, OnDestroy } from '@angular/core';
import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Router } from '@angular/router';

import { ApiService } from '@shared/services/api.service';
import { GlobalService } from '@shared/services/global.service';
import { ModalService } from '@shared/services/modal.service';

import { WalletInfo } from '@shared/models/wallet-info';
import { TransactionInfo } from '@shared/models/transaction-info';

import { Subscription } from 'rxjs';

import { TransactionDetailsComponent } from '../transaction-details/transaction-details.component';
import { RequestObject, ResponseWrapper, ResponsePayload } from "@shared/services/visualcrypt-dtos";
import { clearInterval } from 'timers';

@Component({
  selector: 'history-component',
  templateUrl: './history.component.html',
  styleUrls: ['./history.component.css'],
})

export class HistoryComponent {
  constructor(private apiService: ApiService, private globalService: GlobalService, private modalService: NgbModal, private genericModalService: ModalService, private router: Router) {}

  public transactions: TransactionInfo[];
  public coinUnit: string;
  public pageNumber: number = 1;
  private polling: any;

  ngOnInit() {
    this.startPolling();
    this.polling = setInterval(this.startPolling.bind(this), 5000);
    this.coinUnit = this.globalService.getCoinUnit();
  }

  ngOnDestroy() {
    clearInterval(this.polling);
  }

  onDashboardClicked() {
    this.router.navigate(['/wallet']);
  }

  private openTransactionDetailDialog(transaction: any) {
    const modalRef = this.modalService.open(TransactionDetailsComponent, { backdrop: "static" });
    modalRef.componentInstance.transaction = transaction;
  }

   

  private getHistory() {

    this.apiService.makeRequest(new RequestObject("history",
        {
          walletName: this.globalService.getWalletName()
         // take: 10
        }),
      this.onGetHistory.bind(this));
  }
  private onGetHistory(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      const histories = responsePayload.responsePayload.history;
      if (histories && histories.length === 1) {
        const history = histories[0];
        if (history.transactionsHistory) {
          this.getTransactionInfo(history.transactionsHistory);
        }
      }
    }
  }
  private getTransactionInfo(transactions: any) {
    this.transactions = [];

    for (let transaction of transactions) {
      let transactionType = transaction.type;
      if (transaction.type === "send") {
        transactionType = "sent";
      }
      let transactionId = transaction.id;
      let transactionAmount = transaction.amount.satoshi;
      let transactionFee;
      if (transaction.fee) {
        transactionFee = transaction.fee;
      } else {
        transactionFee = 0;
      }
      let transactionConfirmedInBlock = transaction.confirmedInBlock;
      let transactionTimestamp = transaction.timestamp;

      this.transactions.push(new TransactionInfo(transactionType, transactionId, transactionAmount, transactionFee, transactionConfirmedInBlock, transactionTimestamp));
    }
  }

  private startPolling() {
    this.getHistory();
  }
}
