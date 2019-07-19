import { Component, OnInit } from '@angular/core';

import { ApiService } from '@shared/services/api.service';
import { GlobalService } from '@shared/services/global.service';
import { ModalService } from '@shared/services/modal.service';

import { WalletInfo } from '@shared/models/wallet-info';

import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { RequestObject, ResponseWrapper, ResponsePayload } from "@shared/services/visualcrypt-dtos";

@Component({
  selector: 'receive-component',
  templateUrl: './receive.component.html',
  styleUrls: ['./receive.component.css'],
})

export class ReceiveComponent {
  constructor(private apiService: ApiService, private globalService: GlobalService, public activeModal: NgbActiveModal, private genericModalService: ModalService) { }

  public address: any = "";
  public qrString: any;
  public copied: boolean = false;
  public showAll = false;
  public allAddresses: any;
  public usedAddresses: string[];
  public unusedAddresses: string[];
  public changeAddresses: string[];
  public pageNumberUsed: number = 1;
  public pageNumberUnused: number = 1;
  public pageNumberChange: number = 1;
  public sidechainEnabled: boolean;
  private errorMessage: string;

  ngOnInit() {
    this.sidechainEnabled = this.globalService.getSidechainEnabled();
    this.getReceiveAddresses();
    this.showAllAddresses();
  }

  public onCopiedClick() {
    this.copied = true;
  }

  public showAllAddresses() {
    this.showAll = true;
  }

  public showOneAddress() {
    this.showAll = false;
  }

  private getReceiveAddresses() {
    this.apiService.makeRequest(new RequestObject("getReceiveAddresses", ""), this.onGetReceiveAddresses.bind(this));
  }
  private onGetReceiveAddresses(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      this.allAddresses = [];
      this.usedAddresses = [];
      this.unusedAddresses = [];
      this.changeAddresses = [];
      this.allAddresses = responsePayload.responsePayload.addresses;

      for (let address of this.allAddresses) {
        if (address.isUsed) {
          this.usedAddresses.push(address.address);
        } else if (address.isChange) {
          this.changeAddresses.push(address.address);
        } else {
          this.unusedAddresses.push(address.address);
        }
      }
    }
  }
}
