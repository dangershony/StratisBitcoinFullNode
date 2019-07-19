"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var NodeStatus = /** @class */ (function () {
    function NodeStatus(agent, version, network, coinTicker, processId, consensusHeight, blockStoreHeight, inboundPeers, outbountPeers, enabledFeatures, dataDirectoryPath, runningtime, difficulty, protocolVersion, testnet, relayFee, state) {
        this.agent = agent;
        this.version = version;
        this.network = network;
        this.coinTicker = coinTicker;
        this.processId = processId;
        this.consensusHeight = consensusHeight;
        this.blockStoreHeight = blockStoreHeight;
        this.inboundPeers = inboundPeers;
        this.outbountPeers = outbountPeers;
        this.enabledFeatures = enabledFeatures;
        this.dataDirectoryPath = dataDirectoryPath;
        this.runningTime = runningtime;
        this.difficulty = difficulty;
        this.protocolVersion = protocolVersion;
        this.testnet = testnet;
        this.relayFee = relayFee;
        this.state = state;
    }
    return NodeStatus;
}());
exports.NodeStatus = NodeStatus;
var Peer = /** @class */ (function () {
    function Peer(version, remoteSocketEndpoint, tipHeight) {
        this.version = version;
        this.remoteSocketEndpoint = remoteSocketEndpoint;
        this.tipHeight = tipHeight;
    }
    return Peer;
}());
//# sourceMappingURL=node-status.js.map