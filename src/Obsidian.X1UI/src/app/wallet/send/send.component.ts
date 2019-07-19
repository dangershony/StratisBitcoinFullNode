import { Component, OnInit, OnDestroy, Input } from '@angular/core';
import { FormGroup, FormControl, Validators, FormBuilder, AbstractControl } from '@angular/forms';

import { ApiService } from '@shared/services/api.service';
import { GlobalService } from '@shared/services/global.service';
import { ModalService } from '@shared/services/modal.service';
import { CoinNotationPipe } from '@shared/pipes/coin-notation.pipe';
import { NumberToStringPipe } from '@shared/pipes/number-to-string.pipe';

import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

import { FeeEstimation } from '@shared/models/fee-estimation';
import { TransactionBuilding } from '@shared/models/transaction-building';
import { TransactionSending } from '@shared/models/transaction-sending';

import { SendConfirmationComponent } from './send-confirmation/send-confirmation.component';

import { Subscription } from 'rxjs';
import { debounceTime, window } from 'rxjs/operators';

import { RequestObject, ResponseWrapper, ResponsePayload } from "@shared/services/visualcrypt-dtos";

@Component({
  selector: 'send-component',
  templateUrl: './send.component.html',
  styleUrls: ['./send.component.css'],
})

export class SendComponent implements OnInit, OnDestroy {
  @Input() address: string;
  constructor(private apiService: ApiService, private globalService: GlobalService, private modalService: NgbModal, private genericModalService: ModalService, public activeModal: NgbActiveModal, private fb: FormBuilder) {
    this.buildSendForm();
  }

  public sendForm: FormGroup;
  public sendToSidechainForm: FormGroup;
  public sidechainEnabled: boolean;
  public hasOpReturn: boolean;
  public coinUnit: string;
  public isSending: boolean = false;
  public estimatedFee: number = 0;
  public estimatedSidechainFee: number = 0;
  public totalBalance: number = 0;
  public spendableBalance: number = 0;
  public apiError: string;
  public firstTitle: string;
  public secondTitle: string;
  public opReturnAmount: number = 1;
  private transactionHex: string;
  private responseMessage: any;
  private transaction: TransactionBuilding;
  private walletBalanceSubscription: Subscription;

  ngOnInit() {
    this.sidechainEnabled = this.globalService.getSidechainEnabled();
    if (this.sidechainEnabled) {
      this.firstTitle = "Sidechain";
      this.secondTitle = "Mainchain";
    } else {
      this.firstTitle = "Mainchain";
      this.secondTitle = "Sidechain";
    }
    this.startSubscriptions();
    this.coinUnit = this.globalService.getCoinUnit();
    if (this.address) {
      this.sendForm.patchValue({ 'address': this.address })
    }
  }

  ngOnDestroy() {
    this.cancelSubscriptions();
  };

  private buildSendForm(): void {
    this.sendForm = this.fb.group({
      "address": ["", Validators.compose([Validators.required, Validators.minLength(26)])],
      "amount": ["", Validators.compose([Validators.required, Validators.pattern(/^([0-9]+)?(\.[0-9]{0,8})?$/), Validators.min(0.00001), (control: AbstractControl) => Validators.max((this.spendableBalance - this.estimatedFee) / 100000000)(control)])],
      "fee": ["medium", Validators.required],
      "password": ["", Validators.required]
    });

    this.sendForm.valueChanges.pipe(debounceTime(300))
      .subscribe(data => this.onSendValueChanged(data));
  }




  onSendValueChanged(data?: any) {
    if (!this.sendForm) { return; }
    const form = this.sendForm;
    for (const field in this.sendFormErrors) {
      this.sendFormErrors[field] = '';
      const control = form.get(field);
      if (control && control.dirty && !control.valid) {
        const messages = this.sendValidationMessages[field];
        for (const key in control.errors) {
          this.sendFormErrors[field] += messages[key] + ' ';
        }
      }
    }

    this.apiError = "";

    if (this.sendForm.get("address").valid && this.sendForm.get("amount").valid) {
      this.estimateFee();
    }
  }



  sendFormErrors = {
    'address': '',
    'amount': '',
    'fee': '',
    'password': ''
  };

  sendValidationMessages = {
    'address': {
      'required': 'An address is required.',
      'minlength': 'An address is at least 26 characters long.'
    },
    'amount': {
      'required': 'An amount is required.',
      'pattern': 'Enter a valid transaction amount. Only positive numbers and no more than 8 decimals are allowed.',
      'min': "The amount has to be more or equal to 0.00001.",
      'max': 'The total transaction amount exceeds your spendable balance.'
    },
    'fee': {
      'required': 'A fee is required.'
    },
    'password': {
      'required': 'Your password is required.'
    }
  };

  sendToSidechainFormErrors = {
    'destionationAddress': '',
    'federationAddress': '',
    'amount': '',
    'fee': '',
    'password': ''
  };

  sendToSidechainValidationMessages = {
    'destinationAddress': {
      'required': 'An address is required.',
      'minlength': 'An address is at least 26 characters long.'
    },
    'federationAddress': {
      'required': 'An address is required.',
      'minlength': 'An address is at least 26 characters long.'
    },
    'amount': {
      'required': 'An amount is required.',
      'pattern': 'Enter a valid transaction amount. Only positive numbers and no more than 8 decimals are allowed.',
      'min': "The amount has to be more or equal to 1.",
      'max': 'The total transaction amount exceeds your spendable balance.'
    },
    'fee': {
      'required': 'A fee is required.'
    },
    'password': {
      'required': 'Your password is required.'
    }
  };

  public getMaxBalance() {
    let data = {
      walletName: this.globalService.getWalletName(),
      feeType: this.sendForm.get("fee").value
    }

    let balanceResponse;

    this.apiService.getMaximumBalance(data)
      .subscribe(
        response => {
          balanceResponse = response;
        },
        error => {
          this.apiError = error.error.errors[0].message;
        },
        () => {
          this.sendForm.patchValue({ amount: +new CoinNotationPipe().transform(balanceResponse.maxSpendableAmount) });
          this.estimatedFee = balanceResponse.fee;
        }
      )
  };

  public estimateFee() {
    const feeEstimation = new FeeEstimation(null, null, this.sendForm.get("address").value.trim(), this.sendForm.get("amount").value, this.sendForm.get("fee").value, true);
    this.apiService.makeRequest(new RequestObject("estimateFee", feeEstimation), this.onEstimateFee.bind(this));
  }

  private onEstimateFee(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      this.estimatedFee = responsePayload.responsePayload.satoshi;
    } else {
      this.apiError = responsePayload.statusText;
    }
  }

  public buildTransaction() {
    this.transaction = new TransactionBuilding(
      this.globalService.getWalletName(),
      "account 0",
      this.sendForm.get("password").value,
      this.sendForm.get("address").value.trim(),
      this.sendForm.get("amount").value,
      //this.sendForm.get("fee").value,
      // TO DO: use coin notation
      this.estimatedFee / 100000000,
      true,
      false
    );
    this.apiService.makeRequest(new RequestObject("buildTransaction", this.transaction), this.onBuildTransaction.bind(this));
  };

  private onBuildTransaction(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      this.estimatedFee = responsePayload.responsePayload.fee;
      this.transactionHex = responsePayload.responsePayload.hex;
      if (this.isSending) {
        this.hasOpReturn = false;
        this.sendTransaction(this.transactionHex);
      }
    } else {
      this.isSending = false;
      this.apiError = responsePayload.statusText;
    }
  }

  

  public send() {
    this.isSending = true;
    this.buildTransaction();
  };

  

  private sendTransaction(hex: string) {
    const transaction = new TransactionSending(hex);
    this.apiService.makeRequest(new RequestObject("sendTransaction", transaction), this.onSendTransaction.bind(this));
  }

  private onSendTransaction(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      this.activeModal.close("Close clicked");
      this.openConfirmationModal();
    } else {
      this.isSending = false;
      this.apiError = responsePayload.statusText;
    }
  }

  private getWalletBalance() {

    this.apiService.makeRequest(new RequestObject("balance", ""),
      this.onGetWalletBalance.bind(this));
  }
  private onGetWalletBalance(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      const walletBalance = responsePayload.responsePayload;
      this.totalBalance = walletBalance.amountConfirmed.satoshi + walletBalance.amountUnconfirmed.satoshi;
      this.spendableBalance = walletBalance.spendableAmount.satoshi;
    }
  }

  private openConfirmationModal() {
    const modalRef = this.modalService.open(SendConfirmationComponent, { backdrop: "static" });
    modalRef.componentInstance.transaction = this.transaction;
    modalRef.componentInstance.transactionFee = this.estimatedFee ? this.estimatedFee : this.estimatedSidechainFee;
    modalRef.componentInstance.sidechainEnabled = this.sidechainEnabled;
    modalRef.componentInstance.opReturnAmount = this.opReturnAmount;
    modalRef.componentInstance.hasOpReturn = this.hasOpReturn;
  }

  private cancelSubscriptions() {
    if (this.walletBalanceSubscription) {
      this.walletBalanceSubscription.unsubscribe();
    }
  };

  private startSubscriptions() {
    this.getWalletBalance();
  }
}
