using CL.SQLite.Services;
using Monolith.FireWall.WebUI.Features.Users.Models;

namespace Monolith.FireWall.WebUI.Features.Users.Repositories;

/// <summary>
/// User repository using CL.SQLite Repository pattern
/// </summary>
public class UserRepository
{
    private readonly Repository<UserEntity> _repository;
    private readonly QueryBuilder<UserEntity> _queryBuilder;

    public UserRepository(CL.SQLite.SQLiteLibrary sqlite)
    {
        _repository = sqlite.GetRepository<UserEntity>();
        _queryBuilder = sqlite.GetQueryBuilder<UserEntity>();
    }

    public async Task<UserEntity?> GetByIdAsync(int id)
    {
        var result = await _repository.GetByIdAsync(id);
        return result.IsSuccess ? result.Value : null;
    }

    public async Task<List<UserEntity>> GetAllAsync()
    {
        var result = await _repository.GetAllAsync();
        return result.IsSuccess ? result.Value : new List<UserEntity>();
    }

    public async Task<UserEntity?> GetByUsernameAsync(string username)
    {
        var result = await _queryBuilder
            .Where(u => u.Username == username)
            .FirstOrDefaultAsync();

        return result.IsSuccess ? result.Value : null;
    }

    public async Task<int> CreateAsync(UserEntity user)
    {
        var result = await _repository.InsertAsync(user);
        return result.IsSuccess ? (int)result.Value : 0;
    }

    public async Task<bool> UpdateAsync(UserEntity user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        var result = await _repository.UpdateAsync(user);
        return result.IsSuccess;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var result = await _repository.DeleteAsync(id);
        return result.IsSuccess;
    }
}
