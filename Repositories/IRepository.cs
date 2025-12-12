using System.Linq.Expressions;

namespace WebReader.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate,
        params Expression<Func<T, object>>[] includes);

    Task<IEnumerable<T>> AllAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);
    Task<T> AddAsync(T entity);
}
