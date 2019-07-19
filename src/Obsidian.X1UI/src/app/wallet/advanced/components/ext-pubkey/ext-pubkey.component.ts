import { Component, OnInit } from '@angular/core';
import { ApiService } from '@shared/services/api.service';
import { GlobalService } from '@shared/services/global.service';
import { ModalService } from '@shared/services/modal.service';
import { RequestObject, ResponseWrapper, ResponsePayload } from "@shared/services/visualcrypt-dtos";
import { FormGroup, FormControl, Validators, FormBuilder } from '@angular/forms';

@Component({
  selector: 'app-ext-pubkey',
  templateUrl: './ext-pubkey.component.html',
  styleUrls: ['./ext-pubkey.component.css']
})
export class ExtPubkeyComponent implements OnInit {
  constructor(private apiService: ApiService, private globalService: GlobalService, private genericModalService: ModalService, private fb: FormBuilder) { }

  public message: string;
  public importedAddresses: string[];

  public message2: string;
  public export: string;

  public dumpForm: FormGroup;

  ngOnInit() {
    this.buildForm();
  }
  private passphraseValidator = Validators.compose([Validators.required]);

  private buildForm() {
    this.dumpForm = this.fb.group({
      "walletPassword": ["", this.passphraseValidator],
      "dump": []
    });

    this.dumpForm.valueChanges
      .subscribe(data => this.onValueChanged(data));

    this.onValueChanged();
  }

  onValueChanged(data?: any) {
    if (!this.dumpForm) { return; }
    const form = this.dumpForm;
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
  }

  formErrors = {
    'walletPassword': '',
    'dump': ''
  };

  

  validationMessages = {
    'generateAddresses': {
      'required': 'Please paste at least one key.',
      'min': 'Not enough data.',
      'max': 'Too many lines.'
    },
    'walletPassword': {
      'required': 'A passphrase is required',
      'pattern': 'A passphrase must be from 12 to 60 characters long and contain only lowercase and uppercase latin characters and numbers.'
    },
  };

  public onImportClicked() {
    this.importKeys();
  }

  public onExportClicked() {
    this.exportKeys();
  }

  private importKeys() {
    const data: string = this.dumpForm.get("dump").value;
    const pw: string = this.dumpForm.get("walletPassword").value;
    this.apiService.makeRequest(new RequestObject<{ keys: string, walletPassphrase: string }>("importKeys", { keys: data, walletPassphrase: pw}), this.onImportKeys.bind(this));
  }
  private onImportKeys(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      this.message = responsePayload.responsePayload.message;
      this.importedAddresses = responsePayload.responsePayload.importedAddresses;

      this.importedAddresses.forEach(item => {
        console.log(item);
      });

     
    } else {
      this.message = responsePayload.statusText;
    }
  }

  private exportKeys() {
    const pw: string = this.dumpForm.get("walletPassword").value;
    this.apiService.makeRequest(new RequestObject<{ walletPassphrase: string }>("exportKeys", { walletPassphrase: pw }), this.onExportKeys.bind(this));
  }
  private onExportKeys(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      this.export = responsePayload.responsePayload.message;

    } else {
      this.message = responsePayload.statusText;
      this.export = responsePayload.responsePayload.message;
    }
  }



}
