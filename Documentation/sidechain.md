
## [draft]

Sidechain technical proposal 
------------------------------

A proposal that is based on two way pegs and the drivechain approach.

A mechanism of miner voting will be used, the vote will be on adding allowed sidechains and on deposits from a sidechain where the voting is done for a fixed amount of blocks and a percentage of positive votes indicate success.
Note: This model will work for both POS and POW (as oppose to SPV Proofs that use work to verify a withdraw is valid)

The economy of sidechains can get very complicated so we stick to the following rules:
- A tree structure - a parent chain with sidechains that are children of that parent, sidechin can also have children of thier own.
- Transfers from parent are locked to the sidechain they where sent to (i.e the locked coins can only be unlcoked with transfers back from the chain they where sent to)

## How voting may work
drivechain propose to use OP_RETURN in the coinbase for voting messages in to the chain, this might not be enough as I propose some votes will need more data then OP_RETURN is allowed
I suggest to add a sidechain voting op codes OP_SIDECHAINVOTE, consensus rules will enforce it to be located as a coinbase output with zero value, and contain voting data
[WIP] Use a single op for all votes or create an op per vote

## voting on a sidechain
For a parentchain to accept sidechain deposits, the sidechain itself needs to be succesfuly voted on first.
This solves the problem of the parent chain that does not know about a later created sidechain, sidechains should add the parent when creating the sidechain.

voted sidechain will go in the D1 db (as proposed by drivechain with maybe some differences) and will stay there as an entry.
[WIP] we may decide to add the ability to delete a sidechain

the db structure will be as following
(similar to drivechain with some changes)

- sidechain name
- sidechain identifier (can this just be the genesis?)
- genesis of the sidechain
- number of blocks a withdraw trx must be burred under before it can be voted on
- denimination of the currency 
- checksum of software hash at a given version (may not be needed)
- voting start block - the block height from when voting will start (this is to allow miner to prepare)
- voting period - how many blocks to vote on (do we need this? should there be a minimum?)
- votes - how many positive votes the sidechain got
- deposited period - the time allowed for the deposit to be added to the blockchain (after that the entry in d2 will be deleted)

voting on a sidechain is either yes or no, and note it should not be hard to vote on a sidechain. so we propose that an absent of a vote counts as a yes and 95% of votes in the voting period must be yes (this can of course be configurable)

here as well there are two types of messages
- add a sidechain to d1 (m1) - its not decided if only miners are allowed to suggest sidechains
- vote on a sidechain already in d1 (m2) - coinbase

if by the end of the voting period the sidechain is not approved it will be deleted from d1

## voting on deposits
here I propose some differences from drivechain
mainly one difference is the presence of an SPV Proof in the withdraw vote
this will allow miners that want to vote to not track the full sidechain just the headers to verify a trx is in the sidechain

A suggested rule: when a parent chain sends coins to a sidechain (locks some coins) the sidechain must be in the approved list

vote on deposits also have a db (d2)

- sidechain identifier - must exist in d1 to be included in d2
- deposit identifier
- transaction on the sidechain (with index of output even though we may enforce only one locking output in a trx for sidechain withdraws)
- blockheader - of where the trx was confirmed
- Merkle proof of the existence of the trx in the block
- vote count - how many positive votes (or negative this should be allowed to be negative)

miner votes can be yes/no/none no message means a vote of zero
I would suggest a threshold of 60% to be considered success (the reason for 60% difficulty is such that if a too low value is used this might allow a small group of malicious miners to approve bad withdraws, too high a value will make it very hard to deposit sidechain transactions assuming not all miners will want to track a sidechain)

once the vote is success the deposit transaction may be broadcast to the chain
its important to note that a deposit trx must spend locked coins (coins either locked in genesis or in a past withdraws out of the chain)
the condition to include such a trs in a block is the existence of a success vote in the d2 and so every node can verify that condition

Note: when creating a locked withdraw trx intended to be deposited on a sidechain, the locked on the chain it was created will specify the target address on the target chain and the target chain genesis (this will lock the deposit to only one chain)

the deposit trx will make note to the entry in d2 and consensus rules will enforce the target scriptpubkey and amount are similar
note: the trx must spend any remaining locked coins to a new locked output with the rest of the amount

A new op code is proposed for the withdraw lock (perhaps it might be same as the blockstream op code OP_WITHDRAWVERIFY)


## jurney

on parent chain we create a withdraw that locks coins to a sidechain (assume the sidechain was voted and approved) and to a specific address

structure of withdraw:
scriptsig=<sig> <pubkey>
scriptpubkey=op_withdraw op_drop <address> <sidechain-genesis>

on the sidecahin voting starts for that withdraw, once approved

note: we assume coins have been locked on the sidechain, as a sidechain can only have one parnet the coins must be locked to that parent.
structure of locked coins in sidechain genesis:
scriptsig=<empty>
scriptpubkey=op_deposit <parent-genesis>

create a trx on the sidechain that spends locked coins (with two outputs one to the target address second to lock the rest of the coins)
Its importantt to note that the script language is not enough to verify the trx
in the consensus rules a check must be made that the trx in the parent (in the op_return data) is indeed successfuly voted on and the address is correct
structure the deposit trx:
scriptsig= <parent-genesis> op_equal op_return <voted-trxid-index-parent-chain>
scriptpubkey=op_dup op_hash160 <pubkeyhash> op_equalverify op_checksig
scriptpubkey=op_deposit <parent-genesis>

to send coins back to the parent we lock the coins to a deposit trx 
scriptsig=<sig> <pubkey>
scriptpubkey=op_deposit op_drop <address> <parent-genesis>

on the parent the trx is voted on and if success we pick one or several of the locked outputs to that sidechain
again script language alone is not enough to verify the trx additional checks must be made  in the consensus rules
scriptsig=<sidechain-genesis> op_equal op_return <voted-trxid-index-parent-chain>
scriptsig=<sidechain-genesis> op_equal op_return <voted-trxid-index-parent-chain>
scriptpubkey=op_dup op_hash160 <pubkeyhash> op_equalverify op_checksig
scriptpubkey=op_withdraw <sidechain-genesis>

in the consensus rules if anh of the op codes are detected op_withdraw/op_deposit this is a sidechain trx and must get extra validation