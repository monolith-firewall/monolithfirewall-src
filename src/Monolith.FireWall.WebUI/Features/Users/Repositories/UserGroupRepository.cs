using CL.SQLite.Services;
using Monolith.FireWall.WebUI.Features.Users.Models;

namespace Monolith.FireWall.WebUI.Features.Users.Repositories;

/// <summary>
/// User group repository using CL.SQLite
/// </summary>
public class UserGroupRepository
{
    private readonly Repository<UserGroupEntity> _repository;
    private readonly QueryBuilder<UserGroupEntity> _queryBuilder;
    private readonly Repository<UserGroupMemberEntity> _memberRepository;
    private readonly QueryBuilder<UserGroupMemberEntity> _memberQueryBuilder;

    public UserGroupRepository(CL.SQLite.SQLiteLibrary sqlite)
    {
        _repository = sqlite.CreateRepository<UserGroupEntity>();
        _queryBuilder = sqlite.CreateQueryBuilder<UserGroupEntity>();
        _memberRepository = sqlite.CreateRepository<UserGroupMemberEntity>();
        _memberQueryBuilder = sqlite.CreateQueryBuilder<UserGroupMemberEntity>();
    }

    public async Task<UserGroupEntity?> GetByIdAsync(int id)
    {
        var result = await _repository.GetByIdAsync(id);
        return result.IsSuccess ? result.Data : null;
    }

    public async Task<List<UserGroupEntity>> GetAllAsync()
    {
        var result = await _repository.GetAllAsync();
        return result.IsSuccess ? result.Data : new List<UserGroupEntity>();
    }

    public async Task<UserGroupEntity?> GetByNameAsync(string name)
    {
        var result = await _queryBuilder
            .Where(g => g.Name == name)
            .FirstOrDefaultAsync();

        return result.IsSuccess ? result.Data : null;
    }

    public async Task<int> CreateAsync(UserGroupEntity group)
    {
        group.CreatedAt = DateTime.UtcNow;
        group.UpdatedAt = DateTime.UtcNow;
        var result = await _repository.InsertAsync(group);
        return result.IsSuccess ? (int)result.Data : 0;
    }

    public async Task<bool> UpdateAsync(UserGroupEntity group)
    {
        group.UpdatedAt = DateTime.UtcNow;
        var result = await _repository.UpdateAsync(group);
        return result.IsSuccess;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        // First remove all memberships
        await RemoveAllMembersAsync(id);
        
        var result = await _repository.DeleteAsync(id);
        return result.IsSuccess;
    }

    // Group membership methods
    public async Task<List<int>> GetUserGroupIdsAsync(int userId)
    {
        // Get all memberships for this user
        var allMembers = await _memberRepository.GetAllAsync();
        if (!allMembers.IsSuccess)
            return new List<int>();

        return allMembers.Data
            .Where(m => m.UserId == userId)
            .Select(m => m.GroupId)
            .ToList();
    }

    public async Task<List<UserGroupEntity>> GetUserGroupsAsync(int userId)
    {
        var groupIds = await GetUserGroupIdsAsync(userId);
        if (groupIds.Count == 0)
            return new List<UserGroupEntity>();

        var groups = new List<UserGroupEntity>();
        foreach (var groupId in groupIds)
        {
            var group = await GetByIdAsync(groupId);
            if (group != null)
                groups.Add(group);
        }
        return groups;
    }

    public async Task<bool> AddUserToGroupAsync(int userId, int groupId)
    {
        // Check if already a member
        var existing = await _memberQueryBuilder
            .Where(m => m.UserId == userId && m.GroupId == groupId)
            .FirstOrDefaultAsync();

        if (existing.IsSuccess && existing.Data != null)
            return true; // Already a member

        var member = new UserGroupMemberEntity
        {
            UserId = userId,
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _memberRepository.InsertAsync(member);
        return result.IsSuccess;
    }

    public async Task<bool> RemoveUserFromGroupAsync(int userId, int groupId)
    {
        var result = await _memberQueryBuilder
            .Where(m => m.UserId == userId && m.GroupId == groupId)
            .FirstOrDefaultAsync();

        if (result.IsSuccess && result.Data != null)
        {
            var deleteResult = await _memberRepository.DeleteAsync(result.Data.Id);
            return deleteResult.IsSuccess;
        }

        return false;
    }

    public async Task RemoveAllMembersAsync(int groupId)
    {
        // Get all memberships and delete those for this group
        var allMembers = await _memberRepository.GetAllAsync();
        if (allMembers.IsSuccess)
        {
            foreach (var member in allMembers.Data.Where(m => m.GroupId == groupId))
            {
                await _memberRepository.DeleteAsync(member.Id);
            }
        }
    }

    public async Task<List<int>> GetGroupUserIdsAsync(int groupId)
    {
        // Get all memberships and filter by group
        var allMembers = await _memberRepository.GetAllAsync();
        if (!allMembers.IsSuccess)
            return new List<int>();

        return allMembers.Data
            .Where(m => m.GroupId == groupId)
            .Select(m => m.UserId)
            .ToList();
    }
}
