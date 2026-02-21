using GenDes_OpenRoads.Models;

namespace GenDes_OpenRoads.Services
{
    public interface IAlignmentService
    {
        CivilObjectResult CreateOrUpdateFromPIs(AlignmentRequestFromPIs request);
        CivilObjectResult CreateOrUpdateFromElements(AlignmentRequestFromElements request);
    }
}
