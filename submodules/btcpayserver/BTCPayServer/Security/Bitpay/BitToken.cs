using System;
using NBitpayClient;

namespace BTCPayServer.Security.Bitpay
{
    public class BitTokenEntity
    {
        public string Value
        {
            get; set;
        }
        public string StoreId
        {
            get; set;
        }
        public string Label
        {
            get; set;
        }
        public DateTimeOffset PairingTime
        {
            get; set;
        }
        public string SIN
        {
            get;
            set;
        }

        public BitTokenEntity Clone(Facade facade)
        {
            return new BitTokenEntity()
            {
                Label = Label,
                StoreId = StoreId,
                PairingTime = PairingTime,
                SIN = SIN,
                Value = Value
            };
        }
    }
}
