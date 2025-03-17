public class DataAnalysisService
{
    private readonly YourDbContext _dbContext;

    public DataAnalysisService(YourDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public decimal CalculateTotalRevenueForBank(int bankId)
    {
        return _dbContext.Transactions
            .Where(t => _dbContext.Laundromat
                .Any(l => l.bId == bankId && l.kId == t.LaundromatId))
            .Sum(t => (decimal)t.amount);
    }
}