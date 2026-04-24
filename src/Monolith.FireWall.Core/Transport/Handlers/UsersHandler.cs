using System.Text.Json;
using Monolith.FireWall.Common.Models;
using Monolith.FireWall.Core.Models;
using Monolith.FireWall.Core.Services;

namespace Monolith.FireWall.Core.Transport.Handlers;

public sealed class UsersHandler : ICoreRequestHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        "users.list",
        "users.get",
        "users.create",
        "users.update",
        "users.delete",
        "users.login",
        "users.theme.get",
        "users.theme.update",
        "users.password.change",
        "users.groups.list",
        "users.groups.get",
        "users.groups.create",
        "users.groups.update",
        "users.groups.delete",
        "users.groups.users",
        "users.groups.adduser",
        "users.groups.removeuser",
        "users.permissions"
    };

    public bool CanHandle(string action) => Actions.Contains(action);

    public async Task<ApiResponse> HandleAsync(CoreRequestContext context, JsonElement request, CancellationToken cancellationToken)
    {
        var action = request.GetProperty("action").GetString() ?? string.Empty;

        switch (action)
        {
            // ─────────────────────────────────────────────
            // User CRUD
            // ─────────────────────────────────────────────

            case "users.list":
                var users = await context.UserManager.ListUsersAsync();
                return new ApiResponse(true, users, null);

            case "users.get":
                if (!CoreRequestParsing.TryGetPayload(request, out UserIdRequest getUserRequest, out var getUserError))
                    return new ApiResponse(false, null, getUserError);

                var user = await context.UserManager.GetUserAsync(getUserRequest.Id);
                return user == null
                    ? new ApiResponse(false, null, "User not found")
                    : new ApiResponse(true, user, null);

            case "users.create":
                if (!CoreRequestParsing.TryGetPayload(request, out UserCreateRequest createRequest, out var createError))
                    return new ApiResponse(false, null, createError);

                var createResult = await context.UserManager.CreateUserAsync(createRequest);
                return createResult.Success
                    ? new ApiResponse(true, createResult.User, null)
                    : new ApiResponse(false, null, createResult.Error ?? "Failed to create user");

            case "users.update":
                if (!CoreRequestParsing.TryGetPayload(request, out UserUpdateRequest updateRequest, out var updateError))
                    return new ApiResponse(false, null, updateError);

                var updateResult = await context.UserManager.UpdateUserAsync(updateRequest);
                return updateResult.Success
                    ? new ApiResponse(true, updateResult.User, null)
                    : new ApiResponse(false, null, updateResult.Error ?? "Failed to update user");

            case "users.delete":
                if (!CoreRequestParsing.TryGetPayload(request, out UserIdRequest deleteRequest, out var deleteError))
                    return new ApiResponse(false, null, deleteError);

                var deleted = await context.UserManager.DeleteUserAsync(deleteRequest.Id);
                return deleted
                    ? new ApiResponse(true, new { id = deleteRequest.Id }, null)
                    : new ApiResponse(false, null, "Failed to delete user. Cannot delete the last admin.");

            // ─────────────────────────────────────────────
            // Authentication
            // ─────────────────────────────────────────────

            case "users.login":
                if (!CoreRequestParsing.TryGetPayload(request, out UserLoginRequest loginRequest, out var loginError))
                    return new ApiResponse(false, null, loginError);

                var loginResult = await context.UserManager.ValidateLoginAsync(loginRequest.Username, loginRequest.Password);
                return new ApiResponse(true, new UserLoginResponse
                {
                    Success = loginResult.Success,
                    User = loginResult.User,
                    Permissions = loginResult.Permissions,
                    Error = loginResult.Success ? null : "Invalid username or password"
                }, null);

            // ─────────────────────────────────────────────
            // Theme & Password
            // ─────────────────────────────────────────────

            case "users.theme.get":
                if (!CoreRequestParsing.TryGetPayload(request, out UserIdRequest themeGetRequest, out var themeGetError))
                    return new ApiResponse(false, null, themeGetError);

                var theme = await context.UserManager.GetUserThemeAsync(themeGetRequest.Id);
                return new ApiResponse(true, new { theme }, null);

            case "users.theme.update":
                if (!CoreRequestParsing.TryGetPayload(request, out UserThemeRequest themeUpdateRequest, out var themeUpdateError))
                    return new ApiResponse(false, null, themeUpdateError);

                var themeUpdated = await context.UserManager.UpdateUserThemeAsync(themeUpdateRequest.UserId, themeUpdateRequest.Theme);
                return themeUpdated
                    ? new ApiResponse(true, new { success = true, theme = themeUpdateRequest.Theme }, null)
                    : new ApiResponse(false, null, "Failed to update theme");

            case "users.password.change":
                if (!CoreRequestParsing.TryGetPayload(request, out UserPasswordChangeRequest pwdRequest, out var pwdError))
                    return new ApiResponse(false, null, pwdError);

                var pwdResult = await context.UserManager.ChangePasswordAsync(pwdRequest.UserId, pwdRequest.CurrentPassword, pwdRequest.NewPassword);
                return pwdResult.Success
                    ? new ApiResponse(true, new { success = true }, null)
                    : new ApiResponse(false, null, pwdResult.Error ?? "Failed to change password");

            // ─────────────────────────────────────────────
            // Group CRUD
            // ─────────────────────────────────────────────

            case "users.groups.list":
                var groups = await context.UserManager.ListGroupsAsync();
                return new ApiResponse(true, groups, null);

            case "users.groups.get":
                if (!CoreRequestParsing.TryGetPayload(request, out UserGroupIdRequest getGroupRequest, out var getGroupError))
                    return new ApiResponse(false, null, getGroupError);

                var group = await context.UserManager.GetGroupAsync(getGroupRequest.Id);
                return group == null
                    ? new ApiResponse(false, null, "Group not found")
                    : new ApiResponse(true, group, null);

            case "users.groups.create":
                if (!CoreRequestParsing.TryGetPayload(request, out UserGroupCreateRequest createGroupRequest, out var createGroupError))
                    return new ApiResponse(false, null, createGroupError);

                var createGroupResult = await context.UserManager.CreateGroupAsync(createGroupRequest);
                return createGroupResult.Success
                    ? new ApiResponse(true, createGroupResult.Group, null)
                    : new ApiResponse(false, null, createGroupResult.Error ?? "Failed to create group");

            case "users.groups.update":
                if (!CoreRequestParsing.TryGetPayload(request, out UserGroupUpdateRequest updateGroupRequest, out var updateGroupError))
                    return new ApiResponse(false, null, updateGroupError);

                var updateGroupResult = await context.UserManager.UpdateGroupAsync(updateGroupRequest);
                return updateGroupResult.Success
                    ? new ApiResponse(true, updateGroupResult.Group, null)
                    : new ApiResponse(false, null, updateGroupResult.Error ?? "Failed to update group");

            case "users.groups.delete":
                if (!CoreRequestParsing.TryGetPayload(request, out UserGroupIdRequest deleteGroupRequest, out var deleteGroupError))
                    return new ApiResponse(false, null, deleteGroupError);

                var groupDeleted = await context.UserManager.DeleteGroupAsync(deleteGroupRequest.Id);
                return groupDeleted
                    ? new ApiResponse(true, new { id = deleteGroupRequest.Id }, null)
                    : new ApiResponse(false, null, "Failed to delete group");

            // ─────────────────────────────────────────────
            // Group Membership
            // ─────────────────────────────────────────────

            case "users.groups.users":
                if (!CoreRequestParsing.TryGetPayload(request, out UserGroupIdRequest groupUsersRequest, out var groupUsersError))
                    return new ApiResponse(false, null, groupUsersError);

                var groupUsers = await context.UserManager.GetGroupUsersAsync(groupUsersRequest.Id);
                return new ApiResponse(true, groupUsers, null);

            case "users.groups.adduser":
                if (!CoreRequestParsing.TryGetPayload(request, out UserGroupMemberRequest addMemberRequest, out var addMemberError))
                    return new ApiResponse(false, null, addMemberError);

                var added = await context.UserManager.AddUserToGroupAsync(addMemberRequest.UserId, addMemberRequest.GroupId);
                return added
                    ? new ApiResponse(true, new { success = true }, null)
                    : new ApiResponse(false, null, "Failed to add user to group");

            case "users.groups.removeuser":
                if (!CoreRequestParsing.TryGetPayload(request, out UserGroupMemberRequest removeMemberRequest, out var removeMemberError))
                    return new ApiResponse(false, null, removeMemberError);

                var removed = await context.UserManager.RemoveUserFromGroupAsync(removeMemberRequest.UserId, removeMemberRequest.GroupId);
                return removed
                    ? new ApiResponse(true, new { success = true }, null)
                    : new ApiResponse(false, null, "Failed to remove user from group");

            // ─────────────────────────────────────────────
            // Permissions
            // ─────────────────────────────────────────────

            case "users.permissions":
                if (!CoreRequestParsing.TryGetPayload(request, out UserPermissionsRequest permRequest, out var permError))
                    return new ApiResponse(false, null, permError);

                var permissions = await context.UserManager.GetUserEffectivePermissionsAsync(permRequest.UserId);
                return new ApiResponse(true, new { userId = permRequest.UserId, permissions }, null);
        }

        return new ApiResponse(false, null, $"Unhandled action: {action}");
    }
}
