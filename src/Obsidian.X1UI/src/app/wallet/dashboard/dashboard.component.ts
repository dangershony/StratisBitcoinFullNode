import { Component, OnInit, OnDestroy, Input } from '@angular/core';
import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { FormGroup, FormControl, Validators, FormBuilder } from '@angular/forms';

import { ApiService } from '@shared/services/api.service';
import { GlobalService } from '@shared/services/global.service';
import { ModalService } from '@shared/services/modal.service';
import { WalletInfo } from '@shared/models/wallet-info';
import { TransactionInfo } from '@shared/models/transaction-info';

import { SendComponent } from '../send/send.component';
import { ReceiveComponent } from '../receive/receive.component';
import { TransactionDetailsComponent } from '../transaction-details/transaction-details.component';

import { Subscription } from 'rxjs';
import { Router } from '@angular/router';
import { RequestObject, ResponseWrapper, ResponsePayload } from "@shared/services/visualcrypt-dtos";
import { clearInterval } from 'timers';

@Component({
  selector: 'dashboard-component',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})

export class DashboardComponent implements OnInit, OnDestroy {
  constructor(private apiService: ApiService, private globalService: GlobalService, private modalService: NgbModal, private genericModalService: ModalService, private router: Router, private fb: FormBuilder) {
    this.buildStakingForm();
  }

  public sidechainEnabled: boolean;
  public walletName: string;
  public coinUnit: string;
  public confirmedBalance: number;
  public unconfirmedBalance: number;
  public spendableBalance: number;
  public transactionArray: TransactionInfo[];
  private stakingForm: FormGroup;
  private walletBalanceSubscription: Subscription;
  private walletHistorySubscription: Subscription;
  private stakingInfoSubscription: Subscription;
  public stakingEnabled: boolean;
  public stakingActive: boolean;
  public stakingWeight: number;
  public awaitingMaturity: number = 0;
  public netStakingWeight: number;
  public expectedTime: number;
  public dateTime: string;
  public isStarting: boolean;
  public isStopping: boolean;
  public hasBalance: boolean = false;
  private polling: any;

  ngOnInit() {
    this.sidechainEnabled = this.globalService.getSidechainEnabled();
    this.walletName = this.globalService.getWalletName();
    this.coinUnit = this.globalService.getCoinUnit();
    this.startPolling();
    this.polling = setInterval(this.startPolling.bind(this), 5000);
  };

  ngOnDestroy() {
    clearInterval(this.polling);
  }

  private buildStakingForm(): void {
    this.stakingForm = this.fb.group({
      "walletPassword": ["", Validators.required]
    });
  }

  public goToHistory() {
    this.router.navigate(['/segwitwallet/history']);
  }

  public openSendDialog() {
    const modalRef = this.modalService.open(SendComponent, { backdrop: "static", keyboard: false });
  }

  public openReceiveDialog() {
    const modalRef = this.modalService.open(ReceiveComponent, { backdrop: "static", keyboard: false });
  };

  public openTransactionDetailDialog(transaction: TransactionInfo) {
    const modalRef = this.modalService.open(TransactionDetailsComponent, { backdrop: "static", keyboard: false });
    modalRef.componentInstance.transaction = transaction;
  }

  private getWalletBalance() {

    this.apiService.makeRequest(new RequestObject("balance",
      {
        walletName: this.globalService.getWalletName(),
        accountName: "account 0"
      }),
      this.onGetWalletBalance.bind(this));
  }
  private onGetWalletBalance(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      const walletBalance = responsePayload.responsePayload;
      this.confirmedBalance = walletBalance.amountConfirmed.satoshi;
      this.unconfirmedBalance = walletBalance.amountUnconfirmed.satoshi;
      this.spendableBalance = walletBalance.spendableAmount.satoshi;
      if ((this.confirmedBalance + this.unconfirmedBalance) > 0) {
        this.hasBalance = true;
      } else {
        this.hasBalance = false;
      }
    }
  }

  private getHistory() {

    this.apiService.makeRequest(new RequestObject("history",
      {
        walletName: this.globalService.getWalletName(),
        take: 10
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
    this.transactionArray = [];

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

      this.transactionArray.push(new TransactionInfo(transactionType, transactionId, transactionAmount, transactionFee, transactionConfirmedInBlock, transactionTimestamp));
    }
  }

  public startStaking() {
    this.isStarting = true;
    this.isStopping = false;
    const walletData = {
      name: this.globalService.getWalletName(),
      password: this.stakingForm.get('walletPassword').value
    };
    this.apiService.makeRequest(new RequestObject("startStaking", { password: walletData.password }), this.onStartStaking.bind(this));

  }

  private onStartStaking(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      this.stakingEnabled = true;
      this.stakingForm.patchValue({ walletPassword: "" });
    } else {
      this.isStarting = false;
      this.stakingEnabled = false;
      this.stakingForm.patchValue({ walletPassword: "" });
    }
  }


  public stopStaking() {
    this.isStopping = true;
    this.isStarting = false;
    this.apiService.makeRequest(new RequestObject("stopStaking", ""), this.onStopStaking.bind(this));
  }

  private onStopStaking(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
    } this.stakingEnabled = false;
  }

  private getStakingInfo() {
    this.apiService.makeRequest(new RequestObject("stakingInfo", ""), this.onGetStakingInfo.bind(this));
  }
  private onGetStakingInfo(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      const stakingResponse = responsePayload.responsePayload;
      this.stakingEnabled = stakingResponse.enabled;
      this.stakingActive = stakingResponse.staking;
      this.stakingWeight = stakingResponse.weight;
      this.netStakingWeight = stakingResponse.netStakeWeight;
      this.awaitingMaturity = (this.unconfirmedBalance + this.confirmedBalance) - this.spendableBalance;
      this.expectedTime = stakingResponse.expectedTime;
      this.dateTime = this.secondsToString(this.expectedTime);
      if (this.stakingActive) {
        this.isStarting = false;
      } else {
        this.isStopping = false;
      }
    }
  }

  private secondsToString(seconds: number) {
    let numDays = Math.floor(seconds / 86400);
    let numHours = Math.floor((seconds % 86400) / 3600);
    let numMinutes = Math.floor(((seconds % 86400) % 3600) / 60);
    let numSeconds = ((seconds % 86400) % 3600) % 60;
    let dateString = "";

    if (numDays > 0) {
      if (numDays > 1) {
        dateString += numDays + " days ";
      } else {
        dateString += numDays + " day ";
      }
    }

    if (numHours > 0) {
      if (numHours > 1) {
        dateString += numHours + " hours ";
      } else {
        dateString += numHours + " hour ";
      }
    }

    if (numMinutes > 0) {
      if (numMinutes > 1) {
        dateString += numMinutes + " minutes ";
      } else {
        dateString += numMinutes + " minute ";
      }
    }

    if (dateString === "") {
      dateString = "Unknown";
    }

    return dateString;
  }

  private cancelSubscriptions() {
    if (this.walletBalanceSubscription) {
      this.walletBalanceSubscription.unsubscribe();
    }

    if (this.walletHistorySubscription) {
      this.walletHistorySubscription.unsubscribe();
    }

    if (this.stakingInfoSubscription) {
      this.stakingInfoSubscription.unsubscribe();
    }
  }

  private startPolling() {
    this.getWalletBalance();
    this.getHistory();
    if (!this.sidechainEnabled) {
      this.getStakingInfo();
    }
  }
}
