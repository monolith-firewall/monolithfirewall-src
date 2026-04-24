using CodeLogic;
using CL.SQLite.Services;
using Monolith.FireWall.Core.Models;

namespace Monolith.FireWall.Core.Services;

public sealed class GatewayGroupStore
{
    private CL.SQLite.SQLiteLibrary? _sqlite;
    private Repository<GatewayGroupEntity>? _groupRepository;
    private Repository<GatewayGroupMemberEntity>? _memberRepository;

    public GatewayGroupStore()
    {
        Initialize();
    }

    // ========================================================================
    // Gateway Groups
    // ========================================================================

    public async Task<List<GatewayGroupEntity>> GetGroupsAsync()
    {
        if (_groupRepository == null)
        {
            return new List<GatewayGroupEntity>();
        }

        var result = await _groupRepository.GetAllAsync();
        return result.IsSuccess && result.Value != null
            ? result.Value.ToList()
            : new List<GatewayGroupEntity>();
    }

    public async Task<GatewayGroupEntity?> GetGroupAsync(int id)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.GetQueryBuilder<GatewayGroupEntity>();
        var result = await query.Where(g => g.Id == id).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<GatewayGroupEntity?> GetGroupByNameAsync(string name)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.GetQueryBuilder<GatewayGroupEntity>();
        var result = await query.Where(g => g.Name == name).FirstOrDefaultAsync();
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<List<GatewayGroupEntity>> GetEnabledGroupsAsync()
    {
        if (_sqlite == null)
        {
            return new List<GatewayGroupEntity>();
        }

        var query = _sqlite.GetQueryBuilder<GatewayGroupEntity>();
        var result = await query.Where(g => g.Enabled == true).ToListAsync();
        return result.IsSuccess && result.Value != null
            ? result.Value.ToList()
            : new List<GatewayGroupEntity>();
    }

    public async Task<bool> InsertGroupAsync(GatewayGroupEntity group)
    {
        if (_groupRepository == null)
        {
            return false;
        }

        var result = await _groupRepository.InsertAsync(group);
        if (!result.IsSuccess)
        {
            return false;
        }

        group.Id = Convert.ToInt32(result.Value);
        return true;
    }

    public async Task<bool> UpdateGroupAsync(GatewayGroupEntity group)
    {
        if (_groupRepository == null)
        {
            return false;
        }

        var result = await _groupRepository.UpdateAsync(group);
        return result.IsSuccess;
    }

    public async Task<bool> DeleteGroupAsync(int id)
    {
        if (_groupRepository == null || _memberRepository == null)
        {
            return false;
        }

        // Delete all members first
        var members = await GetMembersByGroupAsync(id);
        foreach (var member in members)
        {
            await _memberRepository.DeleteAsync(member.Id);
        }

        var result = await _groupRepository.DeleteAsync(id);
        return result.IsSuccess;
    }

    // ========================================================================
    // Gateway Group Members
    // ========================================================================

    public async Task<List<GatewayGroupMemberEntity>> GetMembersAsync()
    {
        if (_memberRepository == null)
        {
            return new List<GatewayGroupMemberEntity>();
        }

        var result = await _memberRepository.GetAllAsync();
        return result.IsSuccess && result.Value != null
            ? result.Value.ToList()
            : new List<GatewayGroupMemberEntity>();
    }

    public async Task<List<GatewayGroupMemberEntity>> GetMembersByGroupAsync(int groupId)
    {
        if (_sqlite == null)
        {
            return new List<GatewayGroupMemberEntity>();
        }

        var query = _sqlite.GetQueryBuilder<GatewayGroupMemberEntity>();
        var result = await query.Where(m => m.GroupId == groupId).ToListAsync();
        return result.IsSuccess && result.Value != null
            ? result.Value.OrderBy(m => m.Tier).ThenBy(m => m.Priority).ToList()
            : new List<GatewayGroupMemberEntity>();
    }

    public async Task<List<GatewayGroupMemberEntity>> GetMembersByGatewayAsync(int gatewayId)
    {
        if (_sqlite == null)
        {
            return new List<GatewayGroupMemberEntity>();
        }

        var query = _sqlite.GetQueryBuilder<GatewayGroupMemberEntity>();
        var result = await query.Where(m => m.GatewayId == gatewayId).ToListAsync();
        return result.IsSuccess && result.Value != null
            ? result.Value.ToList()
            : new List<GatewayGroupMemberEntity>();
    }

    public async Task<GatewayGroupMemberEntity?> GetMemberAsync(int groupId, int gatewayId)
    {
        if (_sqlite == null)
        {
            return null;
        }

        var query = _sqlite.GetQueryBuilder<GatewayGroupMemberEntity>();
        var result = await query
            .Where(m => m.GroupId == groupId && m.GatewayId == gatewayId)
            .FirstOrDefaultAsync();
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<bool> InsertMemberAsync(GatewayGroupMemberEntity member)
    {
        if (_memberRepository == null)
        {
            return false;
        }

        var result = await _memberRepository.InsertAsync(member);
        if (!result.IsSuccess)
        {
            return false;
        }

        member.Id = Convert.ToInt32(result.Value);
        return true;
    }

    public async Task<bool> UpdateMemberAsync(GatewayGroupMemberEntity member)
    {
        if (_memberRepository == null)
        {
            return false;
        }

        var result = await _memberRepository.UpdateAsync(member);
        return result.IsSuccess;
    }

    public async Task<bool> DeleteMemberAsync(int memberId)
    {
        if (_memberRepository == null)
        {
            return false;
        }

        var result = await _memberRepository.DeleteAsync(memberId);
        return result.IsSuccess;
    }

    public async Task<bool> DeleteMembersByGroupAsync(int groupId)
    {
        if (_memberRepository == null)
        {
            return false;
        }

        var members = await GetMembersByGroupAsync(groupId);
        foreach (var member in members)
        {
            await _memberRepository.DeleteAsync(member.Id);
        }
        return true;
    }

    public async Task<bool> SetMembersAsync(int groupId, List<GatewayGroupMemberEntity> members)
    {
        if (_memberRepository == null)
        {
            return false;
        }

        // Delete existing members
        await DeleteMembersByGroupAsync(groupId);

        // Insert new members
        foreach (var member in members)
        {
            member.GroupId = groupId;
            member.CreatedAt = DateTime.UtcNow;
            var result = await _memberRepository.InsertAsync(member);
            if (!result.IsSuccess)
            {
                return false;
            }
            member.Id = Convert.ToInt32(result.Value);
        }

        return true;
    }

    private void Initialize()
    {
        try
        {
            var sqlite = CodeLogic.Libraries.Get<CL.SQLite.SQLiteLibrary>();
            if (sqlite == null)
            {
                return;
            }

            _sqlite = sqlite;
            _groupRepository = sqlite.GetRepository<GatewayGroupEntity>();
            _memberRepository = sqlite.GetRepository<GatewayGroupMemberEntity>();
        }
        catch
        {
            _sqlite = null;
            _groupRepository = null;
            _memberRepository = null;
        }
    }
}
