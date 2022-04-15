using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Sky.Base.Controllers
{
    /// <summary>
    /// Main Controller handling tracking
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class BaseController : ControllerBase
    {


        /// <summary>
        /// Tracks a flip
        /// </summary>
        /// <param name="flip"></param>
        /// <param name="AuctionId"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("/ping")]
        public async Task TrackFlip()
        {
            await Task.Delay(1);
        }
    }
}
