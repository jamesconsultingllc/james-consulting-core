using System.Threading.Tasks;

namespace JamesConsulting.Tests;

/// <summary>
/// The Interface interface.
/// </summary>
internal interface IInterface
{
    /// <summary>
    /// The get class by id.
    /// </summary>
    /// <param name="id">
    /// The id.
    /// </param>
    /// <returns>
    /// The <see cref="Task{MyClass}" />.
    /// </returns>
    Task<MyClass> GetClassById(int id);

    /// <summary>
    /// The test.
    /// </summary>
    /// <param name="x">
    /// The x.
    /// </param>
    /// <param name="y">
    /// The y.
    /// </param>
    /// <param name="myClass">
    /// The my class.
    /// </param>
    void Test(int x, string y, MyClass myClass);

    /// <summary>
    /// The test async.
    /// </summary>
    /// <param name="x">
    /// The x.
    /// </param>
    /// <param name="y">
    /// The y.
    /// </param>
    /// <param name="myClass">
    /// The my class.
    /// </param>
    /// <returns>
    /// The <see cref="Task" />.
    /// </returns>
    Task TestAsync(int x, string y, MyClass myClass);
}