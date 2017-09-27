
## [Draft]
Text in [brackets] are comments

still missing 
- DB field size
- Reorg
- Issues

Sidechain technical proposal 
------------------------------

This proposal is based on the Blockstream two-way peg and drivechain.  

**In this document we describe a way to move coins between two chains using miners to vote on validity of withdrawals.**   

##### Miners will vote on two types of messages: 
1. Adding a sidechain 
2. Deposits from a sidechain   

The voting processes is done for a fixed amount of blocks and a percentage of positive votes indicate success.  
*This model will work for both POS and POW (as oppose to SPV Proofs that use work to verify a withdraw is valid)*  

[drivechain has a nice section on why miner voting is a fine share?]

##### We describe two concepts in the context of sidechains.  
- A 'withdraw' - Coins that are locked on a parent chain.    
- A 'deposit' - Coins that are locked on a child chain.     

The economy of sidechains can get very complicated so we stick to the following rules:
- A tree chain structure - a parent chain with sidechains that are children of that parent, sidechain can also have children of their own, transfers are only allowed between parent and its direct child.  
- Transfers are locked to the sidechain they were sent to (i.e. the locked coins can only be unlocked with transfers back from the chain they were sent to)  
- One-way transfer - to support one way transfers a parent can lock coins to a sidechain that has not been voted on (the sidechain has not been added to the parent db)  

## How voting will work
Voting messages will be added to the coinbase by miners as an output, there are two ways vote messages can be added to coinbase
1. Either use the OP_RETURN.  
This might not be enough as I propose some votes will need more data than is allowed in the OP_RETURN.  
2. Use a new OP_SIDECHAIN.  
In that case a new sidechain op code is proposed OP_SIDECHAIN, consensus rules will enforce it to be inside a coinbase output with zero value, and contain voting data, it will behave similar to OP_RETURN, return false from the stack.  
An example of an OP_SIDECHAIN  
```
OP_SIDECHAIN <vote-type> <vote-data>
```

## The vote to add a sidechain
For a parent and child chains to accept sidechain deposits, the sidechain itself needs to first be successfully voted on.  
*This solves the problem of the parent chain that does not know about a later created sidechain.*  
To avoid the need to vote a parent on a new child chains we add a parent to genesis (i.e. chains in the genesis block are included without voting).

Sidechain store:
The sidechain store will be a direct reflection of its relevant messages in coinbase.

The SidechainStore structure:

Field | Size | Description
------|------| -----------
sidechain name | 50 byts | A useful name for the sidechain
sidechain identifier | 32 byts | the sidechain genesis hash
withdraw trashold | uint | how many blocks the withdraw lock must be buried under
denomination | uint | always 1:[denomination] 
voting start block | uint | the block the vote will start on
vote count| uint | yes=1/no=0 (default: yes)
deposite period| uint | the window allowed where the deposit can be added to the blockchain 

**Vote duration and success criteria will be a hard coded values in the chain.**

Voting on a sidechain is either yes or no.  
It should not be hard to vote on a sidechain so we propose that an absent of a vote counts as a yes and 95% of votes in the voting period must be yes.  

**Message type:**  
We define two types of messages in coinbase when voting on a sidechain
```
OP_SIDECHAIN <V1> <name,identifier,trashold,deno,start,perioud,depositeperioud>
```
```
OP_SIDECHAIN <V2> <genesis, vote[1,0,none=1], genesis, vote[1,0,none=1], etc...> 
```

The reason to only allow miners to propose sidechains V1 (not just vote on them) is miner commitment.  
If by the end of the voting period the sidechain is not approved it will be deleted from SidechainStore.  

## The vote to remove a sidechain
[WIP]

## Voting to add a deposit

Sidechains when created will require to add X amount of locked coins in the genesis block (the locked amount depends on the sidechain creator).  
**Note:** Its important to remember this, later we see that we must make sure there is available locked coins on the sidechain, that’s why only one V3 message is allowed per sidechain and only miners can create a V3.

#### We suggest two new op codes for the purpose of transferring coins.   

**op_withdraw** - This is an op that represents coins locked on a parent chain.
**op_deposit** - This is an op that represents coins locked on a child chain.

The behaviour of the op codes is the same as OP_EQUAL (check that two items on the stack are the same) however they get their own op code to mark them as a sidechain message, this will trigger additionl consnesus rules checks.

Coins that are locked in a sidechain use OP_DEPOSIT with the parent genesis. this means only a deposit from that parent can unlock the coins, when coins are locked back (send to the parent chain) they are sent back to an OP_DEPOSIT.
Coins that are locked in a parent use OP_WITHDRAW lock that specifies the address and target sidechain, this can only be unlocked with deposits from that sidechain.

**SPV Proof**  
An SPV Proof is a way of verifying a transaction is included in a block.  
Having an SPV Proof of the withdraw will remove the need for voting miners to track the full parent/child chain and hopefully bring more miners to participate.  

SidechainDepositStore structure

Field | Size | Description
------|----- | -----------
identifier | 32 byts + 4 bytes | trx id and index output that is voted on, we may limit a withdraw to one output and drop to 4 byte.
sidechain identifier | 32 byts | the sidechain genesis hash
voting start block | uint | the block the vote will start
vote count| int | yes=1/no=-1/none=0 (default: none)
withdraw transaction | ? | used for an SPV Proofs
blockheader| 80 bytes | used for an SPV Proofs
Merkle proof| ? | used for an SPV Proofs

**Miner votes:**
- yes = increase the vote count by 1 
- no = decrease the vote count by 1
- none - no vote was found will do nothing to the vote count.

**Vote duration and success criteria will be a hard coded values in the chain.**

**Messages types:**  
We define two types of messages in coinbase when voting on a withdraw
```
OP_SIDECHAIN <V3> <identifier,sidechain-identifier,voting-start,withdraw-transaction,blockheader,merkle-proof>
```
```
OP_SIDECHAIN <V4> <identifier, vote[1,0,-1],identifier, vote[1,0,-1], etc...> 
```

A V3 will create an entry in SidechainDepositStore where vote count will start at zero, if a vote is not passed the success criteria after the vote period the entry will be deleted, if the vote is success the entry will be deleted 1. when the trx is found in a block or 2. the deposit period is reached.  
There can only be one V3 per sidechain vote.  
A V4 message will change the value of vote count (a vote can be negative).  

**Possible success criteria**  
A threshold of 60% can be considered success (the reason for 60% difficulty is such that if a too low value is used this might allow a small group of malicious miners to approve bad withdraws, too high a value will make it very hard to deposit sidechain transactions assuming not all miners will want to track a sidechain)  

**Successful vote**  
Once the vote is a success the deposit transaction may be included in a block within the deposit window (a user can then broadcast and get it included in a block).  
It's important to note that a deposit trx must spend locked coins (coins either locked in genesis or in a past withdraw out of the chain)  
Consensus rules will enforce that such a trx has a successfully voted V3 messages in SidechainDepositStore.  

Note: A withdraw lock must specify the address on the target chain and the target chain genesis.  
A withdraw that is locked to a sidechain can only be unlocked by deposits from that same chain.  

The trx must spend any remaining locked coins to a new locked output with the remaining amount minus fee.  

## Reorg implications

[todo]
How store should behave in a reorg 

## Issues and considerations 
Sidechain is depleted of locked deposits the withdraw will be stuck on the parent.

## Interaction of transactions between two chains

We'll go over a transfer example of the script language on each transaction `parent -> child` and `child -> parent`.

#### Send to sidechain  
On the parent chain, first make sure there are available locked deposits, then create a withdraw that locks coins to a sidechain and to a specific address [consider only allowing 1 withdraw lock per block]

**Create a withdraw transaction on the parent:**  
This coins are going to be locked on the parent.
```
scriptsig=<sig> <pubkey>  
scriptpubkey=<address> op_drop <sidechain-genesis> op_withdraw 
```

**Sidechain genesis**  
Coins have to be locked on the sidechain, as a sidechain can only have one parent the coins must be locked to that parent.  
Structure of locked coins in a sidechain genesis:  
```
scriptsig=<empty>  
scriptpubkey=<parent-genesis> op_deposit
```

**Wait period**  
We wait on the parent for the trx to be buried under enough blocks.  
On the sidechain, a miner will add a V3 message to a coinbase and an entry into SidechainDepositStore, voting starts for that withdraw.  
Once enough V4 messages have approved the V3 entry we can create the deposit transaction.  

**Create a deposit transaction on the sidechain**  
Now we can create a trx on the sidechain that spends locked coins (the trx will have two outputs one to the target address second to lock the rest of the coins) and a reference to the V3 entry in SidechainDepositStore.  
Its important to note that the script language is not enough to verify the trx, in the consensus rules some validation must be done to check that the referenced trx (the V3 message) is indeed successfully voted and the address is correct.  
Structure the deposit trx:  
```
scriptsig=<V3-entry> op_drop <parent-genesis> 
scriptpubkey=op_dup op_hash160 <pubkeyhash> op_equalverify op_checksig  
scriptpubkey=<parent-genesis> op_deposit
```

The P2PKH output can then be spent normally.

#### Send to parent  
Assuming a sidechain was voted successfully on the parent chain.  

To send coins back to the parent first we lock the coins to a deposit trx   
```
scriptsig=<sig> <pubkey>  
scriptpubkey=<address> op_drop <parent-genesis> op_deposit
```

**Wait period** 
We wait on the child for the trx to be buried under enough blocks.  
On the parent chain, an V3 is added in coinbase and to SidechainDepositStore.   
On the parent chain trx is voted on and if success we pick one or several of the locked outputs to that sidechain.  
Note: again, script language alone is not enough to verify the trx additional checks must be made in the consensus rules.    
```
scriptsig=<M3-entry> op_drop <sidechain-genesis>    
scriptsig=<M3-entry> op_drop <sidechain-genesis>   
scriptpubkey=op_dup op_hash160 <pubkeyhash> op_equalverify op_checksig  
scriptpubkey=op_withdraw <sidechain-genesis>  
```

**Consensus rules:**  
If any of the op codes op_withdraw/op_deposit are detected this is a sidechain trx and must get extra validation (i.e. check that the correct V3 entry exists in SidechainDepositStore and that the target address in the V3 trx matches the destination address in the chain trx).

