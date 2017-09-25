
## [draft]
Text surrounded with [this] are comments about changes/uncetainty in the draft proposal
still missing 
- specific db field size
- verification of the script language in the new op codes

Sidechain technical proposal 
------------------------------

A proposal that is based on the Blockstream two way peg and the drivechain approach.

In this spec we describe a mechanism of miner voting. 
Miners will vote on two types of messages 1. adding a sidechain 2. deposits from a sidechain 
The voting processes is done for a fixed amount of blocks and a percentage of positive votes indicate success.
Note: This model will work for both POS and POW (as oppose to SPV Proofs that use work to verify a withdraw is valid)

[drivechain has a nice sections on why miner voting is a fine perhaps adding some links here is a good idea]

Locking coins in a parent/child chain is called a 'withdraw'
Unlocking coins on parent/child is called a 'deposit'

The economy of sidechains can get very complicated so we stick to the following rules:
- A tree chain structure - a parent chain with sidechains that are children of that parent, sidechain can also have children of thier own, transfers are only allowed between parent and child.
- Transfers from parent are locked to the sidechain they where sent to (i.e the locked coins can only be unlocked with transfers back from the chain they where sent to)
- Chains that want to allow one way withdraw pegs can enable locking coins to a sidechian that was not voted (this is only for withdraw a deposit must have a sucessfuly voted chain)

## How voting may work
One solution is to use the OP_RETURN in the coinbase when voting on messages in to the chain, this might not be enough as I propose some votes will need more data then OP_RETURN is allowed.
If the OP_RETURN is not enough a new op code may be used for sidechain voting OP_SIDECHAINVOTE, consensus rules will enforce it to be located as a coinbase output with zero value, and contain voting data, it will act similar to OP_RETURN and return false from the stack.
An example of an OP_SIDECHAINVOTE
```
OP_SIDECHAINVOTE <vote-type> <vote-data>
```

## voting on a sidechain
For a parent chain to accept sidechain deposits, the sidechain itself needs to first be succesfuly voted in to the parent chain.
This solves the problem of the parent chain that does not know about a later created sidechain, sidechains can just add a rule to add the parent chain when creating the sidechain.

Sidechain db D1:
Voted sidechain will go in the D1 and will stay there as an entry.
[WIP] we may decide to add the ability to delete a sidechain.

Sidechain DB D1 structure:

- sidechain name 
- sidechain identifier [can this just be the genesis]
- genesis of the sidechain
- number of blocks a withdraw must be burried under before it can be voted on
- denomination of the currency [to avoid complexity we may say this will always be 1:1]
- voting start block - the block height from when voting will start (this is to allow miner to prepare)
- voting period - how many blocks to vote on (do we need this? should there be a minimum?)
- votes - how many positive votes the sidechain got
- deposited period - the window allowed where the deposit can be added to the blockchain (after that the entry in D2 will be deleted)

Voting on a sidechain is either yes or no. it should not be hard to vote on a sidechain so we propose that an absent of a vote counts as a yes and 95% of votes in the voting period must be yes (this can of course be configurable) then the parent chain can accept deposites from that sidechain.

**Messages** Two types of messages in coinbase (this may use OP_RETURN as well)
```
OP_SIDECHAINVOTE <M1> <name,genesis,bureidunder,deno,start,perioud,depositeperioud>
```
```
OP_SIDECHAINVOTE <M2> <genesis, vote[1,0,none=1]>

[optional approach] 
OP_SIDECHAINVOTE <M2> <genesis, vote[1,0,none=1], genesis, vote[1,0,none=1], etc...> 
```

The reason to only allow miners to propose sidechains M1 (not just vote on them is) is miner commitment.
If by the end of the voting period the sidechain is not approved it will be deleted from D1

## Voting on deposits

A sidechain will be created with X amount of locked coins.
**Note:** Its important to remember this, later we see that we must make sure there is a available locked coins on the sidechain, thats why only one M3 message is allowed per sidechain and only miners can create M3.

Two new op codes are suggested
op_withdraw - this is an op that represents coins locked on a parent chain that are withdrawn
op_deposit [explain more]

Coins that are locked in a sidechain use OP_DEPOSIT op code with the parent genesis. this means only a deposit from that parent can unlock the coins, when coins are locked back (send to the parent chain) they are sent back to an OP_DEPOSIT.
Coins that are locked in a parent use OP_WITHDRAW lock that specifies the address and target sidechain, this can only be unlocked with deposits from that sdeichain.

**SPV Proof**
An SPV Proof is a way of verifying a transaction is part of a block. 
Having an SPV Proof of the withdraw will remove the need for voting miners to track the full parent/child chain and hopefully bring more miners to participate.

Sidechain DB D2 structure
- identifier - the withdraw trx hash and index of the locked output
- sidechain identifier - one of the entries in D1
- voting start block 
- vote count - how many votes this withdraw got
[SPV Proofs]
- withdraw transaction 
- blockheader - of where the trx was confirmed
- Merkle proof of the existence of the trx in the block

Miner votes can be yes/no/none no message means a vote of zero

**Messages** two types of messages in coinbase (this may use OP_RETURN as well)
```
OP_SIDECHAINVOTE <M3> <identifier,sidechain-identifier,voting-start,withdraw-transaction,blockheader,merkle-proof>
```
```
OP_SIDECHAINVOTE <M4> <identifier, vote[1,0,-1]>

[alternatively a multi vote message] 
OP_SIDECHAINVOTE <M4> <identifier, vote[1,0,-1],identifier, vote[1,0,-1], etc...> 
```

An M3 will create an entry in D2 where vote count is zero, if vote failed after the vote perioud the entry will be deleted, if the vote is success the entry will be deleted with whent he trx is broadast or the deposti period is reached.
There can only be 1 M3 per 
An M4 message will change the value of vote count (a vote goes bellow 0 it will stay zero)

[suggestion]
A suggested threshold of 60% can be considered success (the reason for 60% difficulty is such that if a too low value is used this might allow a small group of malicious miners to approve bad withdraws, too high a value will make it very hard to deposit sidechain transactions assuming not all miners will want to track a sidechain)

Once the vote is success the deposit transaction may be broadcast to the chain within the deposit window.
Its important to note that a deposit trx must spend locked coins (coins either locked in genesis or in a past withdraws out of the chain)
The condition to include such a trx in a block is the existence of a success vote in D2 (as a result every node can verify that condition)

Note: A withdraw lock must specify the address on the target chain and the target chain genesis.
A withdraw that is locked to a sidechain can only be unlocked by deposits from that same chain.

**Note:** The trx must spend any remaining locked coins to a new locked output with the remaning amount.


## Reorg implcations

[todo]
How DB D1 and D2 should behave in a reorg 

## Interaction of tranactions between two chains

**Send to sidechain**
On the parent chain, firt make sure there are available locked deposits, then create a withdraw that locks coins to a sidechain and to a specific address  [consider only allowing 1 withdraw lock per block]

Structure of withdraw:  
```
scriptsig=<sig> <pubkey>  
scriptpubkey=<address> op_drop <sidechain-genesis> op_withdraw 
```

Now on the sidecahin M3 is added to a coinbase and an entry in D2 voting starts for that withdraw.
Once enough M4 messages have approved the M3 entry we can deposit.

Note: Coins have to be locked on the sidechain, as a sidechain can only have one parent the coins must be locked to that parent.  
Structure of locked coins in sidechain genesis:  
```
scriptsig=<empty>  
scriptpubkey=<parent-genesis> op_deposit
```

Now we can create a trx on the sidechain that spends locked coins (the trx will have two outputs one to the target address second to lock the rest of the coins) and will have a reference to the M3 entry in D2. 
Its importantt to note that the script language is not enough to verify the trx, in the consensus rules some validation must be done to check that the referenced trx is indeed successfuly voted on and the address is correct.
Structure the deposit trx:  
```
scriptsig=<M3 entry> op_drop <parent-genesis> 
scriptpubkey=op_dup op_hash160 <pubkeyhash> op_equalverify op_checksig  
scriptpubkey=<parent-genesis> op_deposit
```

**Send to parent**
Assuming a sidechain was voted successfully on the parent chain

Sending coins back to the parent we lock the coins to a deposit trx   
```
scriptsig=<sig> <pubkey>  
scriptpubkey=op_drop <address> <parent-genesis> op_deposit
```

Now on the parent chain an M3 is added in coinbase and to D2 db.
On the parent chain trx is voted on and if success we pick one or several of the locked outputs to that sidechain.
Note: again script language alone is not enough to verify the trx additional checks must be made  in the consensus rules  
```
scriptsig=<M3 entry> op_drop <sidechain-genesis> op_equal   [do we need an op code?]  
scriptsig=<M3 entry> op_drop <sidechain-genesis> op_equal   [do we need an op code?]  
scriptpubkey=op_dup op_hash160 <pubkeyhash> op_equalverify op_checksig  
scriptpubkey=op_withdraw <sidechain-genesis>  
```
Consnensu rules: if any of the op codes are detected op_withdraw/op_deposit this is a sidechain trx and must get extra validation (i.e. check athat the corect M3 exists in the D2 db.



