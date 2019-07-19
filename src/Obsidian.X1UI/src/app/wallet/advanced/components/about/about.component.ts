import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { ApiService } from '@shared/services/api.service';
import { ModalService } from '@shared/services/modal.service';
import { NodeStatus } from '@shared/models/node-status';
import { GlobalService } from '@shared/services/global.service';
import { RequestObject, ResponseWrapper, ResponsePayload } from "@shared/services/visualcrypt-dtos";
import { clearInterval } from 'timers';

@Component({
  selector: 'app-about',
  templateUrl: './about.component.html',
  styleUrls: ['./about.component.css']
})
export class AboutComponent implements OnInit, OnDestroy {

  constructor(private globalService: GlobalService, private apiService: ApiService, private genericModalService: ModalService) { }

  private nodeStatusSubscription: Subscription;
  public clientName: string;
  public applicationVersion: string;
  public fullNodeVersion: string;
  public network: string;
  public protocolVersion: number;
  public blockHeight: number;
  public dataDirectory: string;
  private polling: any;

  ngOnInit() {
    this.applicationVersion = this.globalService.getApplicationVersion();
    this.polling = setInterval(this.getNodeStatus.bind(this), 5000);
    this.getNodeStatus();
  }

  ngOnDestroy() {
    clearInterval(this.polling);
  }

  private getNodeStatus() {
    this.apiService.makeRequest(new RequestObject("nodeStatus", ""), this.onGetNodeStatus.bind(this));
  }
  private onGetNodeStatus(responsePayload: ResponsePayload) {
    if (responsePayload.status === 200) {
      const nodeStatus: NodeStatus = responsePayload.responsePayload;
      this.clientName = nodeStatus.agent;
      this.fullNodeVersion = nodeStatus.version;
      this.network = nodeStatus.network;
      this.protocolVersion = nodeStatus.protocolVersion;
      this.blockHeight = nodeStatus.blockStoreHeight;
      this.dataDirectory = nodeStatus.dataDirectoryPath;
      }
    }
  

}
