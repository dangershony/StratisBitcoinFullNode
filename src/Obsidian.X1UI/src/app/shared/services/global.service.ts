import {Injectable} from "@angular/core";
import { ElectronService } from 'ngx-electron';

@Injectable({
  providedIn: 'root'
})
export class GlobalService {
  constructor(private electronService: ElectronService) {
    this.setApplicationVersion();
    this.setTestnetEnabled();
    this.setDaemonIP(null);
  }

  private applicationVersion: string = "1.1.1";
  private testnet: boolean = false;
  private sidechain: boolean = false;
  private walletPath: string;
  private currentWalletName: string;
  private coinUnit: string;
  private network: string;
  private daemonIP: string;


  getApplicationVersion() {
    return this.applicationVersion;
  }

  setApplicationVersion() {
    if (this.electronService.isElectronApp) {
      this.applicationVersion = this.electronService.remote.app.getVersion();
    }
  }

  getTestnetEnabled() {
    return this.testnet;
  }

  setTestnetEnabled() {
    if (this.electronService.isElectronApp) {
      this.testnet = this.electronService.ipcRenderer.sendSync('get-testnet');
    }
  }

  getSidechainEnabled() {
    return this.sidechain;
  }

  

  getApiPort() {
    return 37777;
  }

  

  getWalletPath() {
    return this.walletPath;
  }

  setWalletPath(walletPath: string) {
    this.walletPath = walletPath;
  }

  getNetwork() {
    return this.network;
  }

  setNetwork(network: string) {
    this.network = network;
  }

  getWalletName() {
    return this.currentWalletName;
  }

  setWalletName(currentWalletName: string) {
    this.currentWalletName = currentWalletName;
  }

  getCoinUnit() {
    return this.coinUnit;
  }

  setCoinUnit(coinUnit: string) {
    this.coinUnit = coinUnit;
  }

  getDaemonIP() {
    return this.daemonIP;
  }

  setDaemonIP(ipAddress: string) {
    if (ipAddress) {
      this.daemonIP = ipAddress;
    } else {
      if (this.electronService.isElectronApp) {
        this.daemonIP = this.electronService.ipcRenderer.sendSync('get-daemonip');
      } else {
        this.daemonIP = 'localhost';
      }
    }
   
  }
}
