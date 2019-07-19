import { Component, OnInit } from '@angular/core';
import { FormGroup, Validators, FormBuilder } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';

import { ApiService } from '@shared/services/api.service';
import { ModalService } from '@shared/services/modal.service';

import { WalletCreation } from '@shared/models/wallet-creation';
import { SecretWordIndexGenerator } from './secret-word-index-generator';
import { GlobalService } from '@shared/services/global.service';
import { RequestObject, ResponseWrapper, ResponsePayload } from "@shared/services/visualcrypt-dtos";

@Component({
  selector: 'app-confirm-mnemonic',
  templateUrl: './confirm-mnemonic.component.html',
  styleUrls: ['./confirm-mnemonic.component.css']
})
export class ConfirmMnemonicComponent implements OnInit {

  public secretWordIndexGenerator = new SecretWordIndexGenerator();

  constructor(private apiService: ApiService, private genericModalService: ModalService, private route: ActivatedRoute, private router: Router, private fb: FormBuilder, private globalService: GlobalService) {
    this.buildMnemonicForm();
  }
  private newWallet: WalletCreation;
  private subscription: Subscription;
  public mnemonicForm: FormGroup;
  public matchError: string = "";
  public isCreating: boolean;

  ngOnInit() {
    this.subscription = this.route.queryParams.subscribe(params => {
      this.newWallet = new WalletCreation(
        params["name"],
        params["mnemonic"],
        params["password"],
        params["passphrase"]
      );
    });
  }
  private passphraseValidator = Validators.compose([
    Validators.required,
    Validators.minLength(12),
    Validators.maxLength(60),
    Validators.pattern(/^[a-zA-Z0-9]*$/)
  ]);

  private buildMnemonicForm(): void {
    this.mnemonicForm = this.fb.group({
      "word1": ["", this.passphraseValidator]
    });

    this.mnemonicForm.valueChanges
      .subscribe(data => this.onValueChanged(data));

    this.onValueChanged();
  }

  onValueChanged(data?: any) {
    if (!this.mnemonicForm) { return; }
    const form = this.mnemonicForm;
    for (const field in this.formErrors) {
      this.formErrors[field] = '';
      const control = form.get(field);
      if (control && control.dirty && !control.valid) {
        const messages = this.validationMessages[field];
        for (const key in control.errors) {
          this.formErrors[field] += messages[key] + ' ';
        }
      }
    }

    this.matchError = "";
  }

  formErrors = {
    'word1': '',
    'word2': '',
    'word3': ''
  };

  validationMessages = {
    'word1': {
      'required': 'This secret word is required.',
      'minlength': 'The passphrase must be at least one character long',
      'maxlength': 'The passphrase can not be longer than 60 characters',
      'pattern': 'Only latin uppercase and lowercase characters and numbers allowed.'
    }
  };

  public onConfirmClicked() {
    this.checkMnemonic();
    if (this.checkMnemonic()) {
      this.isCreating = true;
      this.createWallet(this.newWallet);
    }
  }

  public onBackClicked() {
    this.router.navigate(['/setup/create/show-mnemonic'], { queryParams : { name: this.newWallet.name, mnemonic: this.newWallet.mnemonic, password: this.newWallet.password, passphrase: this.newWallet.passphrase }});
  }

  private checkMnemonic(): boolean {
    if (this.mnemonicForm.get('word1').value.trim() === this.newWallet.password) {
      return true;
    } else {
      this.matchError = 'The passphrase is not correct.';
      return false;
    }
  }

  private createWallet(wallet: WalletCreation) {
    this.apiService.makeRequest(new RequestObject<WalletCreation>("createWallet", wallet), this.onCreateWallet.bind(this));
  }
  private onCreateWallet(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      this.genericModalService.openModal("Wallet Created", "Your wallet has been created.<br>Keep passphrase safe and <b>make a backup of your wallet<b>!");
      this.router.navigate(['']);
    } else {
      this.isCreating = false;
      this.matchError = responsePayload.statusText;
    }
  }
}
