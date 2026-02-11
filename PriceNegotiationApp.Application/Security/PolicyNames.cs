namespace PriceNegotiationApp.Application.Security
{
    public class PolicyNames
    {
        public const string Create = nameof(Create);
        public const string Read = nameof(Read);
        public const string Update = nameof(Update);
        public const string Delete = nameof(Delete);

        public const string ModifyNegotiationAsOwner = nameof(ModifyNegotiationAsOwner);

        public const string AdminOrStaffOrOwner = nameof(AdminOrStaffOrOwner);
        public const string Owner = nameof(Owner);

    }
}
