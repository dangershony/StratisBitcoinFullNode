import { Component, OnInit } from '@angular/core';
import { FormGroup, Validators, FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';

import { GlobalService } from '@shared/services/global.service';
import { ApiService } from '@shared/services/api.service';
import { ModalService } from '@shared/services/modal.service';

import { WalletLoad } from '@shared/models/wallet-load';

import { RequestObject, ResponseWrapper, ResponsePayload } from "@shared/services/visualcrypt-dtos";


// extend window for vcl
declare global {
  interface Window { clientKeyPair: any; }
}

window.clientKeyPair = window.clientKeyPair || {};
// end extend window for vcl
@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})

export class LoginComponent implements OnInit {
  constructor(private globalService: GlobalService, private apiService: ApiService, private genericModalService: ModalService, private router: Router, private fb: FormBuilder) {
    this.buildDecryptForm();
  }

  public sidechainEnabled: boolean;
  public hasWallet: boolean = true;
  public isDecrypting = false;
  private openWalletForm: FormGroup;
  private wallets: string[];

  public authKey: string = ApiService.serverAuthKeyHex

  public info: string;

  ngOnInit() {
    this.getWalletFiles();
  }

  private hostValidator = Validators.compose([
    Validators.required,
    Validators.minLength(1),
    Validators.maxLength(24),
    Validators.pattern(/^([0-9]{1,3})[.]([0-9]{1,3})[.]([0-9]{1,3})[.]([0-9]{1,3})$/)
  ]);

  private buildDecryptForm(): void {
    this.openWalletForm = this.fb.group({
      "selectWallet": [{ value: "", disabled: this.isDecrypting }, Validators.required],
      "host": [{ value: "127.0.0.1", disabled: this.isDecrypting }, this.hostValidator]
    });

    this.openWalletForm.valueChanges
      .subscribe(data => this.onValueChanged(data));

    this.onValueChanged();
  }

  onValueChanged(data?: any) {
    if (!this.openWalletForm) { return; }
    const form = this.openWalletForm;
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
        this.wallets = [];
        if (control.valid) {
          console.log("Setting host to " + control.value);
          this.globalService.setDaemonIP(control.value);
          this.getWalletFiles();
        } else {

        }
       
      }
    }
  }
 

  formErrors = {
     'host': 'Invalid host name.'
  };

  validationMessages = {
    'host': {
      'required': "Please enter a host, e.g. 'localhost'",
      'pattern': "Invalid IP address."
    }
  };

  private getWalletFiles() {
    if (!ApiService.serverPublicKey) {
      setTimeout(this.getWalletFiles.bind(this), 1000);
      return;
    }
    this.apiService.makeRequest(new RequestObject("getWalletFiles", ""), this.onGetWalletFiles.bind(this));
  }

  onGetWalletFiles(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      this.wallets = responsePayload.responsePayload.walletsFiles;
      this.globalService.setWalletPath(responsePayload.responsePayload.walletsPath);
      if (this.wallets.length > 0) {
        for (let wallet in this.wallets) {
          this.wallets[wallet] = this.wallets[wallet].slice(0, -12);
        }
      } else {
      }
    } else {
      if (responsePayload.status === 401) {
        this.info = "User name and password are required for this node.";
      }
      setTimeout(this.getWalletFiles.bind(this), 1000);
    }
   
  }
  private loadWallet(walletLoad: WalletLoad) {
    this.apiService.makeRequest(new RequestObject("loadWallet", walletLoad), this.onLoadWallet.bind(this));
  }

  private onLoadWallet(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      this.router.navigate(['wallet/dashboard']);
    } else {
      this.isDecrypting = false;
    }
  }

  public onCreateClicked() {
    this.router.navigate(['setup']);
  }

  public onEnter() {
    if (this.openWalletForm.valid) {
      this.onDecryptClicked();
    }
  }

  public onDecryptClicked() {
    this.isDecrypting = true;
    const walletName = this.openWalletForm.get("selectWallet").value;
    this.globalService.setWalletName(walletName);
    let walletLoad = new WalletLoad(walletName, "pw_check_in_login.component_is_not_supported"
      //this.openWalletForm.get("password").value
    );
    this.loadWallet(walletLoad);
  }



  private getNodeStatus() {
    this.apiService.makeRequest(new RequestObject("nodeStatus", ""), this.onGetNodeStatus.bind(this));
  }

  private onGetNodeStatus(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      const nodeStatus = responsePayload.responsePayload;
      this.globalService.setCoinUnit(nodeStatus.coinTicker);
      this.globalService.setNetwork(nodeStatus.network);
    }
  }
}
