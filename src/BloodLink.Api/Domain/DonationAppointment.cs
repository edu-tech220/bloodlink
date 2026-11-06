namespace BloodLink.Api.Domain;

public class DonationAppointment
{
    public int Id { get; set; }
    public int DonorId { get; set; }
    public Donor? Donor { get; set; }
    public DateTime ScheduledAt { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public int? ResultingBloodUnitId { get; set; }
    public BloodUnit? ResultingBloodUnit { get; set; }
}
