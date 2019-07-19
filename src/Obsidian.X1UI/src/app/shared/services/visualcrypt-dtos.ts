export class RequestObject<T> {
  constructor(name: string, payload: T) {
    this.command = name;
    this.payload = JSON.stringify(payload);
  }

  public command: string;
  public payload: string;
  public target: string;

}
export class ResponseWrapper {
  public currentPublicKey: string;
  public authKey: string;
  public cipherV2Bytes: string;
};
export class ResponsePayload {
  public responsePayload: any;
  public status: number;
  public statusText: string;
}
