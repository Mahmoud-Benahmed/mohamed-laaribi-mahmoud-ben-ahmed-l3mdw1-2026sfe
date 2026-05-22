using ERP.AuthService.Application.Interfaces.Repositories;
using ERP.AuthService.Domain;
using ERP.AuthService.Infrastructure.Security;
using MongoDB.Driver;

namespace ERP.AuthService.Infrastructure.Persistence.Repositories
{
    public class RefreshTokenRepository : BaseRepository<RefreshToken>,IRefreshTokenRepository
    {
        private readonly IRefreshTokenHashingHelper _refreshTokenHashingHelper;

        public RefreshTokenRepository(MongoDbContext context, IRefreshTokenHashingHelper refreshTokenHashing) 
            :base(context, CollectionNames.RefreshTokens) { }

        public async Task AddAsync(RefreshToken token)
            => await _collection.InsertOneAsync(token);

        public async Task<RefreshToken?> GetByTokenAsync(string rawToken)
        {
            string hash = _refreshTokenHashingHelper.Hash(rawToken);  // ← hash before querying
            return await _collection
                .Find(WithTenant(Builders<RefreshToken>.Filter.Eq(t => t.Token, hash)))
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(RefreshToken refreshToken)
        {
            UpdateDefinition<RefreshToken> update = Builders<RefreshToken>.Update
                .Set(x => x.IsRevoked, refreshToken.IsRevoked)
                .Set(x => x.RevokedAt, refreshToken.RevokedAt);

            await _collection.UpdateOneAsync(
                x => x.Token == refreshToken.Token,
                update
            );
        }


        public async Task RevokeAllByUserIdAsync(Guid userId)
        {
            UpdateDefinition<RefreshToken> update = Builders<RefreshToken>
                .Update
                .Set(x => x.IsRevoked, true)
                .Set(x => x.RevokedAt, DateTime.UtcNow);

            await _collection.UpdateManyAsync(
                x => x.UserId == userId && !x.IsRevoked,
                update);
        }

        public async Task DeleteAllAsync()
        {
            await _collection.DeleteManyAsync(FilterDefinition<RefreshToken>.Empty);
        }
    }

}
