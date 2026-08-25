namespace PriceNegotiationApp.Modules.Negotiations.Domain;

internal enum NegotiationStatus
{
    Open = 1,
    Accepted = 2,

    /// <summary>Terminal. Reached only via auto-rejection of an over-limit counter-proposal.</summary>
    Rejected = 3,

    /// <summary>Terminal. Owner withdrew; row and history are preserved.</summary>
    Withdrawn = 4,
}
