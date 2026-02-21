using GenDes_OpenRoads.Models;

namespace GenDes_OpenRoads.Services
{
    public interface IProfileService
    {
        CivilObjectResult CreateOrUpdateFromPIs(ProfileRequestFromPIs request);
        CivilObjectResult CreateOrUpdateFromSegments(ProfileRequestFromSegments request);
    }
}
