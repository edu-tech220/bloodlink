namespace BloodLink.Api.Domain;

public class Donor
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public BloodType BloodType { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string Phone { get; set; } = string.Empty;
    public DateOnly? LastDonationDate { get; set; }

    public List<DonationAppointment> Appointments { get; set; } = new();
}
