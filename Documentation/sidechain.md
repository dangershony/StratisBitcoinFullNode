
[draft]

Sidechain technical proposal 
------------------------------

A proposal that is based on two way pegs and the drivechain approach

drivechain propose to use a mechanism of voting on deposits from a sidechain where the voting is done for a fixed amount of blocks and a percentage of positive votes indicate an approved transfer
this model will work for both POS and POW (as oppose to the blockstream proposal where SPV Proofs requires a withdraw to be buried under enough POW to be valid)

# How voting may work
drivechain propose to use OP_RETURN in the coinbase for voting messages in to the chain, this might not be enough as I propose some votes will need more data then OP_RETURN is allowed
I suggest to add a sidechain voting op codes OP_SIDECHAINVOTE that will only enforce to be located as a coinbase output with zero value, and contain voting data
[WIP] Use a single op for all votes or create an op per vote

# voting on a sidechain
Similar to the drivechain approach, for a parentchain to accept sidechain deposits, the sidechain itself needs to be approved first using the voting mechanism and be successfully approved.
This also solves the problem of the parent chain that does not know about a later created sidechain

voted sidechain will go in the D1 db (as proposed by drivechain with maybe some differences) and will stay there as an entry 
[WIP] we may decide to add the ability to delete a sidechain

the db structure will be as following
(similar to drivechain with some changes)

- sidechain name
- sidechain identifier (can this just be the genesis?)
- genesis of the sidechain
- number of blocks a withdraw trx must be burred under before it can be voted on
- checksum of software hash at a given version (may not be needed)
- voting start block - the block height from when voting will start (this is to allow miner to prepare)
- voting period - how many blocks to vote on (do we need this? should there be a minimum?)
- deposited period - the time allowed for the deposit to be added to the blockchain (after that the entry in d2 will be deleted)

voting on a sidechain is either yes or no, and note it should not be hard to vote on a sidechain. so we propose that an absent of a vote counts as a yes and 95% of votes in the voting period must be yes (this can of course be configurable)

here as well there are two types of messages
- add a sidechain to d1 (m1)
- vote on a sidechain already in d1 (m2)

if by the end of the voting period the sidechain is not approved it will be deleted from d1

# voting on deposits
here I propose some differences from drivechain
mainly one difference is the presence of an SPV Proof in the withdraw vote
this will allow miners that want to vote to not track the full sidechain just the headers to verify a trx is in the sidechain

vote on deposits also have a db (d2)

- sidechain identifier - must exist in d1 to be included in d2
- deposit identifier
- transaction on the sidechain (with index of output even though we may enforce only one locking output in a trx for sidechain withdraws)
- blockheader - of where the trx was confirmed
- Merkle proof of the existence of the trx in the block
- vote count - how many positive votes (or negative this should be allowed to be negative)

miner votes can be yes/no/none no message means a vote of zero
I would suggest a threshold of 60% to be considered success (the reason for 60% difficulty is such that if a too low value is used this might allow a small grouped of malicious miners to approve bad withdraws, too high a value will make it very hard to deposit sidechain transactions assuming not all miner will want to track a sidechain)

once the vote is success the deposit transaction may be broadcast to the chain
its important to note that a deposit trx must spend locked coins (coins either locked in genesis or in a past withdraws out of the chain)
the condition to include such a trs in a block is the existence of a success vote in the d2 and so every node can verify that condition

Note: when creating a locked withdraw trx intended to be deposited on a sidechain, the locked on the chain it was created will specify the target address on the target chain and the target chain genesis (this will lock the deposit to only one chain)

the deposit trx will make note to the entry in d2 and consensus rules will enforce the target scriptpubkey and amount are similar
note: the trx must spend any remaining locked coins to a new locked output with the rest of the amount

A new op code is proposed for the withdraw lock (perhaps it might be same as the blockstream op code OP_WITHDRAWVERIFY)


