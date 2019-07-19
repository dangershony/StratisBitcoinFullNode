import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Title } from '@angular/platform-browser';

import { Subscription } from 'rxjs';
import { retryWhen, delay, tap } from 'rxjs/operators';

import { ApiService } from '@shared/services/api.service';
import { ElectronService } from 'ngx-electron';
import { GlobalService } from '@shared/services/global.service';

import { NodeStatus } from '@shared/models/node-status';
import { RequestObject, ResponseWrapper, ResponsePayload } from "@shared/services/visualcrypt-dtos";
import { setTimeout } from 'timers';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
})

export class AppComponent implements OnInit, OnDestroy {
  constructor(private router: Router, private apiService: ApiService, private globalService: GlobalService, private titleService: Title, private electronService: ElectronService) { }

  private readonly maxRetryCount = 60 * 60 * 3;
  private readonly tryDelayMilliseconds = 1000;
  private retries: number = 0;
  loading = true;
  loadingFailed = false;

  private polling: any;
  private isDestroyed: boolean = false;

  ngOnInit() {
    this.loadServerPrivateKey();
    this.polling = setInterval(this.loadServerPrivateKey.bind(this), this.tryDelayMilliseconds);
    this.setTitle();
  }

  ngOnDestroy() {
    if (this.isDestroyed)
      return;
    clearInterval(this.polling);
    this.loading = false;
    this.isDestroyed = true;
  }

  private loadServerPrivateKey() {
    this.apiService.makeRequest(new RequestObject<string>("getKey", ""), this.onLoadServerPrivateKey.bind(this));
  }

  private onLoadServerPrivateKey(responsePayload: ResponsePayload) {
    if ((responsePayload.status !== 200 || !ApiService.serverPublicKey) && this.retries < this.maxRetryCount) {
      this.retries++;
    } else {
      if (!ApiService.serverPublicKey) {
        console.log("Failed to get the server's public key after " + this.retries + " retries, giving up.");
        this.loading = false;
        this.loadingFailed = true;
       
      } else {
        console.log("Received the server's public key.");
        this.ngOnDestroy();
        this.router.navigate(["/login"]);
      }
    }
  }




  private setTitle() {
    const applicationName = "ObsidianX";
    const testnetSuffix = this.globalService.getTestnetEnabled() ? ' (testnet)' : '';
    const title = `${applicationName} ${this.globalService.getApplicationVersion()}${testnetSuffix}`;

    this.titleService.setTitle(title);
  }

  public openSupport() {
    this.electronService.shell.openExternal('https://github.com/stratisproject/StratisCore/');
  }
}
