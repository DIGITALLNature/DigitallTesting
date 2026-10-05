// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

namespace Digitall.Dataverse.Testing;

public class FakeDataverseOptions
{
    public Guid UserId { get; set; } = Guid.Empty;
    public Guid BusinessUnitId { get; set; } = Guid.Empty;
    public DateOnly? FiscalYearStart { get; set; }
    public int MaxRetrieveCount { get; set; } = 5000;
    public string OrganizationVersion { get; set; } = "9.2.0.0";
    public Guid OrganizationId { get; set; } = Guid.Empty;
    public string OrganizationUniqueName { get; set; } = "org";
    public string OrganizationFriendlyName { get; set; } = "Fake Organization";
}
