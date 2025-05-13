using IXICore;
using IXICore.RegNames;

namespace SPIXI.MiniApps.ActionRequestModels
{
    public class RegNameAction<T> : MiniAppActionBase
    {
        public T data;
        public Address? feeRecipientAddress;
        public IxiNumber? feeAmount;
        public RegisteredNameRecord? nameRecord;
        public List<RegisteredNameDataRecord>? nameDataRecords;

        public RegNameAction(T data, string? feeRecipientAddress, string? feeAmount, byte[]? nameRecord, byte[][]? nameDataRecords)
        {
            this.data = data;
            if (feeRecipientAddress != null)
            {
                this.feeRecipientAddress = new Address(feeRecipientAddress);
            }

            if (feeAmount != null)
            {
                this.feeAmount = new IxiNumber(feeAmount);
            }

            if (nameRecord != null)
            {
                this.nameRecord = new RegisteredNameRecord(nameRecord);
            }

            if (nameDataRecords != null)
            {
                this.nameDataRecords = new();
                foreach (var record in nameDataRecords)
                {
                    this.nameDataRecords.Add(new RegisteredNameDataRecord(record, true));
                }
            }
        }
    }

    public class RegNameActionBase
    {
        public RegNameInstruction instruction { get; protected set; }
        public string decodedName { get; protected set; }
        public byte[] name { get; protected set; }
        public Address nextPkHash { get; protected set; }
        public byte[] signaturePk { get; protected set; }
        public byte[] signature { get; protected set; }
        public RegNameActionBase(RegNameInstruction instruction, string name, string nextPkHash, byte[] signaturePk, byte[] signature)
        {
            this.instruction = instruction;
            this.decodedName = name;
            this.name = IxiNameUtils.encodeAndHashIxiName(name);
            if (nextPkHash != null)
            {
                this.nextPkHash = new Address(nextPkHash);
            }
            this.signaturePk = signaturePk;
            this.signature = signature;
        }

        protected RegNameActionBase() { }
    }

    public class RegNameRegisterAction : RegNameActionBase
    {
        public uint registrationTimeInBlocks { get; private set; }
        public Address recoveryHash { get; private set; }
        public uint capacity { get; private set; }

        public RegNameRegisterAction(string name, uint registrationTime, uint capacity, string nextPkHash, string recoveryHash)
            : base(RegNameInstruction.register, name, nextPkHash, null, null)
        {
            this.registrationTimeInBlocks = registrationTime;
            this.capacity = capacity;
            this.recoveryHash = new Address(recoveryHash);
        }
    }

    public class RegNameExtendAction : RegNameActionBase
    {
        public uint extensionTimeInBlocks { get; private set; }
        public RegNameExtendAction(string name, uint extensionTimeInBlocks)
            : base(RegNameInstruction.extend, name, null, null, null)
        {
            this.extensionTimeInBlocks = extensionTimeInBlocks;
        }

    }

    public class RegNameChangeCapacityAction : RegNameActionBase
    {
        public uint newCapacity { get; private set; }
        public ulong sequence { get; private set; }

        public RegNameChangeCapacityAction(string name, uint newCapacity, ulong sequence, string nextPkHash, byte[] sigPk, byte[] signature)
            : base(RegNameInstruction.changeCapacity, name, nextPkHash, sigPk, signature)
        {
            this.newCapacity = newCapacity;
            this.sequence = sequence;
        }

    }

    public class RegNameRecoverAction : RegNameActionBase
    {
        public Address newRecoveryHash { get; private set; }
        public ulong sequence { get; private set; }
        public RegNameRecoverAction(string name, ulong sequence, string nextPkHash, string newRecoveryHash, byte[] recoveryPk, byte[] recoverySig)
            : base(RegNameInstruction.recover, name, nextPkHash, recoveryPk, recoverySig)
        {
            this.newRecoveryHash = new Address(newRecoveryHash);
            this.sequence = sequence;
        }

    }

    public class RegNameActionDataRecord
    {
        public string? name { get; private set; }
        public int ttl { get; private set; }
        public string data { get; private set; }
        public byte[]? checksum { get; private set; }

        public RegNameActionDataRecord(string? name, int ttl, string data, byte[]? checksum)
        {
            this.name = name;
            this.ttl = ttl;
            this.data = data;
            this.checksum = checksum;
        }
    }

    public class RegNameUpdateRecordsAction : RegNameActionBase
    {
        public RegNameActionDataRecord[] records { get; private set; }
        public ulong sequence { get; private set; }
        public RegNameUpdateRecordsAction(string name, RegNameActionDataRecord[] records, ulong sequence, string nextPkHash, byte[] pkSig, byte[] signature)
            : base(RegNameInstruction.updateRecord, name, nextPkHash, pkSig, signature)
        {
            this.records = records;
            this.sequence = sequence;
        }

    }

    public class RegNameToggleAllowSubnamesAction : RegNameActionBase
    {
        public bool allowSubnames { get; private set; }
        public IxiNumber fee { get; private set; }
        public Address feeRecipientAddress { get; private set; }
        public ulong sequence { get; private set; }

        public RegNameToggleAllowSubnamesAction(string name, bool allowSubnames, IxiNumber fee, string feeRecipientAddress, ulong sequence, string nextPkHash, byte[] pkSig, byte[] signature)
            : base(RegNameInstruction.toggleAllowSubnames, name, nextPkHash, pkSig, signature)
        {
            this.allowSubnames = allowSubnames;
            this.fee = fee;
            this.feeRecipientAddress = new Address(feeRecipientAddress);
            this.sequence = sequence;
        }

    }
}
