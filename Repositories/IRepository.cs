using System.Linq.Expressions;

namespace WebReader.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> AllAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
}