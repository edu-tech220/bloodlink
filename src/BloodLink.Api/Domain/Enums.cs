namespace BloodLink.Api.Domain;

public enum BloodType
{
    OPositive,
    ONegative,
    APositive,
    ANegative,
    BPositive,
    BNegative,
    ABPositive,
    ABNegative
}

public enum BloodComponent
{
    WholeBlood,
    RedCells,
    Plasma,
    Platelets,
    Cryoprecipitate
}

public enum BloodUnitStatus
{
    Quarantined,
    Available,
    Reserved,
    Issued,
    Discarded,
    Expired
}

public enum AppointmentStatus
{
    Scheduled,
    Completed,
    Cancelled,
    NoShow
}

public enum RequestPriority
{
    Routine,
    Urgent,
    Emergency
}

public enum RequestStatus
{
    Pending,
    PartiallyReserved,
    Reserved,
    Issued,
    Cancelled,
    Rejected
}

public enum UserRole
{
    Admin,
    Hospital
}
