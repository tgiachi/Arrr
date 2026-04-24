namespace Arrr.Core.Interfaces;

/// <summary>
/// Plugin that requires QR-code scanning for authentication (e.g. WhatsApp).
/// The service exposes the pending code via GET /api/plugins/{id}/qr.
/// </summary>
public interface IQrPlugin
{
    /// <summary>Raw QR payload; null when no pairing is in progress.</summary>
    string? PendingQrCode { get; }
}
