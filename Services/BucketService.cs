using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Services;

public class BucketService(BucketRepository bucketRepository)
{
    public async Task RemoveBucketAsync(Bucket? bucket, CancellationToken cancellationToken)
    {
        if (bucket == null) return;

        await bucketRepository.DeleteAsync(bucket.Id, cancellationToken);
    }
}
