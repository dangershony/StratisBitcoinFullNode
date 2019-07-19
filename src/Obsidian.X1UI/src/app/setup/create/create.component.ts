import { Component, OnInit } from '@angular/core';
import { FormGroup, Validators, FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';

import { GlobalService } from '@shared/services/global.service';
import { ApiService } from '@shared/services/api.service';
import { ModalService } from '@shared/services/modal.service';

import { PasswordValidationDirective } from '@shared/directives/password-validation.directive';

import { WalletCreation } from '@shared/models/wallet-creation';

@Component({
  selector: 'create-component',
  templateUrl: './create.component.html',
  styleUrls: ['./create.component.css'],
})

export class CreateComponent implements OnInit {
  constructor(private globalService: GlobalService, private apiService: ApiService, private genericModalService: ModalService, private router: Router, private fb: FormBuilder) {
    this.buildCreateForm();
  }

  public createWalletForm: FormGroup;
  private newWallet: WalletCreation;

  ngOnInit() {

  }

  private hostValidator = Validators.compose([
    Validators.required,
    Validators.pattern(/^([0-9]{1,3})[.]([0-9]{1,3})[.]([0-9]{1,3})[.]([0-9]{1,3})$/)
  ]);

  private passphraseValidator = Validators.compose([
    Validators.required,
    Validators.minLength(12),
    Validators.maxLength(60),
    Validators.pattern(/^[a-zA-Z0-9]*$/)
  ]);

  private nameValidator = Validators.compose([
    Validators.required,
    Validators.minLength(1),
    Validators.maxLength(24),
    Validators.pattern(/^[a-zA-Z0-9]*$/)
  ]);

  private buildCreateForm(): void {
    this.createWalletForm = this.fb.group({
      "host": ["127.0.0.1", this.hostValidator],
      "walletName": ["", this.nameValidator],
      "walletPassword": ["", this.passphraseValidator],
      "walletPasswordConfirmation": ["", Validators.required],
      "selectNetwork": ["test", Validators.required]
    }, {
        validator: PasswordValidationDirective.MatchPassword
      });

    this.createWalletForm.valueChanges
      .subscribe(data => this.onValueChanged(data));

    this.onValueChanged();
  }

  onValueChanged(data?: any) {
    if (!this.createWalletForm) { return; }
    const form = this.createWalletForm;
    for (const field in this.formErrors) {
      this.formErrors[field] = '';
      const control = form.get(field);
      if (control && control.dirty && !control.valid) {
        const messages = this.validationMessages[field];
        for (const key in control.errors) {
          this.formErrors[field] += messages[key] + ' ';
        }
      }
      if (control && field === "host") {
        if (control.valid) {
          console.log("Setting host to " + control.value);
          this.globalService.setDaemonIP(control.value);
        } else {

        }

      }
    }
  }

  formErrors = {
    'host': 'Invalid host name.',
    'walletName': '',
    'walletPassword': '',
    'walletPasswordConfirmation': ''
  };

  validationMessages = {
    'host': {
      'required': "Please enter a host, e.g. 'localhost'",
      'pattern': "Invalid IP address."
    },
    'walletName': {
      'required': 'A wallet name is required.',
      'minlength': 'A wallet name must be at least one character long.',
      'maxlength': 'A wallet name cannot be more than 24 characters long.',
      'pattern': 'Please enter a valid wallet name. [a-Z] and [0-9] are the only characters allowed.'
    },
    'walletPassword': {
      'required': 'A passphrase is required',
      'pattern': 'A passphrase must be from 12 to 60 characters long and contain only lowercase and uppercase latin characters and numbers.'
    },
    'walletPasswordConfirmation': {
      'required': 'Confirm your passphrase.',
      'walletPasswordConfirmation': 'Passphrases do not match.'
    }
  };

  public onBackClicked() {
    this.router.navigate(["/setup"]);
  }

  public onCreateClicked() {
    this.newWallet = new WalletCreation(this.createWalletForm.get("walletName").value, "", this.createWalletForm.get("walletPassword").value, "");
    this.router.navigate(['/setup/create/show-mnemonic'], { queryParams: { name: this.newWallet.name, mnemonic: this.newWallet.mnemonic, password: this.newWallet.password, passphrase: this.newWallet.passphrase } });
  }
}

  
