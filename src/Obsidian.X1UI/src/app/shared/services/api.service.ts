import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient, HttpParams, HttpErrorResponse } from '@angular/common/http';
import { Observable, interval, throwError } from 'rxjs';
import { catchError, switchMap, startWith } from 'rxjs/operators';

import { GlobalService } from './global.service';
import { ModalService } from './modal.service';

import { AddressLabel } from '../models/address-label';
import { WalletCreation } from '../models/wallet-creation';
import { WalletRecovery } from '../models/wallet-recovery';
import { WalletLoad } from '../models/wallet-load';
import { WalletInfo } from '../models/wallet-info';
import { SidechainFeeEstimation } from '../models/sidechain-fee-estimation';
import { FeeEstimation } from '../models/fee-estimation';
import { TransactionBuilding } from '../models/transaction-building';
import { TransactionSending } from '../models/transaction-sending';
import { NodeStatus } from '../models/node-status';
import { WalletRescan } from '../models/wallet-rescan';
import { vcl } from '@shared/services/visualcrypt-light.js';
import { setTimeout } from 'timers';
import { RequestObject, ResponseWrapper, ResponsePayload } from "@shared/services/visualcrypt-dtos";

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  constructor(private http: HttpClient, private globalService: GlobalService, private modalService: ModalService, private router: Router) {

  }

  public static serverPublicKey: Uint8Array;
  public static serverAuthKey: Uint8Array;
  public static serverAuthKeyHex: string;
  public static clientPrivateKey: Uint8Array;
  public static clientPublicKey: Uint8Array;



  makeRequest<T>(arg: RequestObject<T>, callback) {
    let request = null;
    console.log(`Begin request: ${arg.command}`);
    if (arg.command === "getKey") {
      const clientKeyPair = vcl.generateKeyPair();
      ApiService.clientPrivateKey = clientKeyPair.private;
      ApiService.clientPublicKey = clientKeyPair.public;
      request = vcl.createModel(ApiService.clientPublicKey);
    } else {
      if (!ApiService.serverPublicKey) {
        throw "No server public key!";
      }
      else {
        arg.target = this.globalService.getWalletName();
        let json = JSON.stringify(arg);
        let jsonBytes = new TextEncoder().encode(json);
        let cipherV2Bytes = vcl.encrypt(jsonBytes, ApiService.serverPublicKey, ApiService.serverAuthKey, ApiService.clientPrivateKey);
        request = vcl.createModel(ApiService.clientPublicKey, cipherV2Bytes);
      }

    }
    console.log("URL: " + this.getApiUrl());
    this.http.post<ResponseWrapper>(this.getApiUrl(), request)
      .subscribe(
        response => {
          ApiService.serverPublicKey = vcl.hexStringToBytes(response.currentPublicKey);
          ApiService.serverAuthKey = vcl.hexStringToBytes(response.authKey);
          ApiService.serverAuthKeyHex = response.authKey;
          if (response.cipherV2Bytes) {
            const decrypted = vcl.decrypt(vcl.hexStringToBytes(response.cipherV2Bytes),
              ApiService.serverPublicKey,
              ApiService.serverAuthKey,
              ApiService.clientPrivateKey);
            const json = new TextDecoder().decode(decrypted);
            const responsePayload: ResponsePayload = JSON.parse(json);
            if (responsePayload.status !== 200) {
              console.log(arg.command + ":" + responsePayload.status + " - " + responsePayload.statusText);
            }
            callback(responsePayload);
          } else {
            const publicKeyPayload = new ResponsePayload();
            publicKeyPayload.responsePayload = response;
            publicKeyPayload.status = 200;
            publicKeyPayload.statusText = "Ok";
            callback(publicKeyPayload);
          }
        },
        error => {
          const errorPayload = new ResponsePayload();
          errorPayload.status = error.status;
          errorPayload.statusText = error.status === 0 ? error.message : error.statusText;
          errorPayload.responsePayload = { errorDescription: error.message }
          callback(errorPayload);
        });
  }


  private getApiUrl() : string {
    return `http://${this.globalService.getDaemonIP()}:${this.globalService.getApiPort()}/SecureApi/ExecuteAsync`;
  }



  getAddressBookAddresses(): Observable<any> {
    console.log("getAddressBookAddresses is not impl.");
    return null;
  //return this.pollingInterval.pipe(
  //  startWith(0),
  //  switchMap(() => this.http.get(this.getApiUrl() + '/AddressBook')),
  //  catchError(err => this.handleHttpError(err))
  //);
}

addAddressBookAddress(data: AddressLabel): Observable < any > {
  return this.http.post(this.getApiUrl() + '/AddressBook/address', JSON.stringify(data)).pipe(
    catchError(err => this.handleHttpError(err))
  );
}

removeAddressBookAddress(label: string): Observable < any > {
  let params = new HttpParams().set('label', label);
  return this.http.delete(this.getApiUrl() + '/AddressBook/address', { params }).pipe(
    catchError(err => this.handleHttpError(err))
  );
}



/** Gets the extended public key from a certain wallet */
getExtPubkey(data: WalletInfo): Observable < any > {
  let params = new HttpParams()
    .set('walletName', data.walletName)
    .set('accountName', 'account 0');

  return this.http.get(this.getApiUrl() + '/segwitwallet/extpubkey', { params }).pipe(
    catchError(err => this.handleHttpError(err))
  );
}

/**
* Get a new mnemonic
*/
getNewMnemonic(): Observable < any > {
  let params = new HttpParams()
    .set('language', 'English')
    .set('wordCount', '12');

  return this.http.get(this.getApiUrl() + '/segwitwallet/mnemonic', { params }).pipe(
    catchError(err => this.handleHttpError(err))
  );
}

/**
 * Create a new Stratis wallet.
 */
createStratisWallet(data: WalletCreation): Observable < any > {
  return this.http.post(this.getApiUrl() + '/segwitwallet/create/', JSON.stringify(data)).pipe(
    catchError(err => this.handleHttpError(err))
  );
}

/**
 * Recover a Stratis wallet.
 */
recoverStratisWallet(data: WalletRecovery): Observable < any > {
  return this.http.post(this.getApiUrl() + '/segwitwallet/recover/', JSON.stringify(data)).pipe(
    catchError(err => this.handleHttpError(err))
  );
}



/**
 * Get wallet status info from the API.
 */
getWalletStatus(): Observable < any > {
  console.log("getWalletStatus()");
  return this.http.get(this.getApiUrl() + '/segwitwallet/status').pipe(
    catchError(err => this.handleHttpError(err))
  );
}




/**
 * Get the maximum sendable amount for a given fee from the API
 */
getMaximumBalance(data): Observable < any > {
  console.log("getMaximumBalance()");
  let params = new HttpParams()
    .set('walletName', data.walletName)
    .set('accountName', "account 0")
    .set('feeType', data.feeType)
    .set('allowUnconfirmed', "true");
  return this.http.get(this.getApiUrl() + '/segwitwallet/maxbalance', { params }).pipe(
    catchError(err => this.handleHttpError(err))
  );
}



/**
* Estimate the fee of a transaction
*/
estimateFee(data: FeeEstimation): Observable < any > {
  console.log("estimateFee()");
  return this.http.post(this.getApiUrl() + '/segwitwallet/estimate-txfee', {
    'walletName': data.walletName,
    'accountName': data.accountName,
    'recipients': [
      {
        'destinationAddress': data.recipients[0].destinationAddress,
        'amount': data.recipients[0].amount
      }
    ],
    'feeType': data.feeType,
    'allowUnconfirmed': true
  }).pipe(
    catchError(err => this.handleHttpError(err))
  );
}





/** Remove transaction */
removeTransaction(walletName: string): Observable < any > {
  console.log("removeTransaction()");
  let params = new HttpParams()
    .set('walletName', walletName)
    .set('all', 'true')
    .set('resync', 'true');
  return this.http.delete(this.getApiUrl() + '/segwitwallet/remove-transactions', { params }).pipe(
    catchError(err => this.handleHttpError(err))
  );
}



/**
 * Start staking
 */
startStaking(data: any): Observable < any > {
  console.log("startStaking()");
  return this.http.post(this.getApiUrl() + '/staking/startstaking', JSON.stringify(data)).pipe(
    catchError(err => this.handleHttpError(err))
  );
}



/**
  * Stop staking
  */
stopStaking(): Observable < any > {
  console.log("stopStaking()");
  return this.http.post(this.getApiUrl() + '/staking/stopstaking', 'true').pipe(
    catchError(err => this.handleHttpError(err))
  );
}

/**
 * Send shutdown signal to the daemon
 */
shutdownNode(): Observable < any > {
  console.log("shutdownNode()");
  return this.http.post(this.getApiUrl() + '/node/shutdown', 'corsProtection:true').pipe(
    catchError(err => this.handleHttpError(err))
  );
}


  private handleHttpError(error: HttpErrorResponse, silent ?: boolean) {
  console.log(error);
  if (error.status === 0) {
    if (!silent) {
      this.modalService.openModal(null, null);
      this.router.navigate(['app']);
    }
  } else if (error.status >= 400) {
    if (!error.error.errors[0].message) {
      console.log(error);
    }
    else {
      this.modalService.openModal(null, error.error.errors[0].message);
    }
  }
  return throwError(error);
}
}
