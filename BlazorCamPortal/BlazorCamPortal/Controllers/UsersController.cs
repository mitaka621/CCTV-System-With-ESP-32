using CamPortal.Contracts.Abstractions.Services;
using CamPortal.Contracts.Constants;
using CamPortal.Contracts.Dtos.UserDtos;
using CamPortal.Contracts.Models;
using CamPortal.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CamPortal.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize(Roles = Roles.Admin)]
    public class UsersController : ControllerBase
    {
        private readonly IUserAuthService _userAuthService;
        private readonly IUserManagementService _userManagementService;

        public UsersController(
            IUserAuthService userAuthService,
            IUserManagementService userManagementService)
        {
            _userAuthService = userAuthService;
            _userManagementService = userManagementService;
        }

        [HttpGet]
        public async Task<ActionResult<List<UserListItemDto>>> GetAll()
            => Ok(await _userManagementService.GetAllUsersAsync());

        [HttpGet("roles")]
        public async Task<ActionResult<List<RoleDto>>> GetAllRoles()
            => Ok(await _userManagementService.GetAllRolesAsync());

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = await _userAuthService.CreateUserAsync(model);

            if (userId == null)
            {
                return Conflict(new { error = "User with the same username or email already exists." });
            }

            return Ok(new { id = userId });
        }

        [HttpPut("{userId:guid}/roles")]
        public async Task<IActionResult> UpdateRoles(Guid userId, [FromBody] UpdateUserRolesModel model)
        {
            if (!ModelState.IsValid || userId != model.UserId)
            {
                return BadRequest(ModelState);
            }

            var currentUserIdClaim = User.FindFirst(CustomClaimTypes.Id)?.Value;

            if (!Guid.TryParse(currentUserIdClaim, out var currentUserId))
            {
                return Unauthorized();
            }

            var result = await _userManagementService.UpdateUserRolesAsync(userId, model.RoleIds, currentUserId);

            return result switch
            {
                UpdateUserRolesResult.Success => Ok(),
                UpdateUserRolesResult.UserNotFound => NotFound(),
                UpdateUserRolesResult.CannotEditSelf => Conflict(new { error = "You cannot edit your own roles." }),
                _ => StatusCode(500),
            };
        }

        [HttpDelete("{userId:guid}")]
        public async Task<IActionResult> Delete(Guid userId)
        {
            var currentUserIdClaim = User.FindFirst(CustomClaimTypes.Id)?.Value;

            if (!Guid.TryParse(currentUserIdClaim, out var currentUserId))
            {
                return Unauthorized();
            }

            var result = await _userManagementService.DeleteUserAsync(userId, currentUserId);

            return result switch
            {
                DeleteUserResult.Success => Ok(),
                DeleteUserResult.UserNotFound => NotFound(),
                DeleteUserResult.CannotDeleteSelf => Conflict(new { error = "You cannot delete your own account." }),
                DeleteUserResult.CannotDeleteLastAdmin => Conflict(new { error = "Cannot delete the last administrator." }),
                _ => StatusCode(500),
            };
        }
    }
}
