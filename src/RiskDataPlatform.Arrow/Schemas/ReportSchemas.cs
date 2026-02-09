using Apache.Arrow;
using Apache.Arrow.Types;

namespace RiskDataPlatform.Arrow.Schemas;

public static class ReportSchemas
{
    public static Schema CreateCoreSchema()
    {
        var fields = new List<Field>
        {
            new Field("TradeId", new StringType(), nullable: false),
            new Field("ExternalRef", new StringType(), nullable: true),
            new Field("BookId", new Int32Type(), nullable: false),
            new Field("BookName", new StringType(), nullable: false),
            new Field("DeskId", new Int32Type(), nullable: false),
            new Field("DeskName", new StringType(), nullable: false),
            new Field("InstrumentType", new StringType(), nullable: false),
            new Field("InstrumentSubType", new StringType(), nullable: true),
            new Field("Counterparty", new StringType(), nullable: true),
            new Field("Currency", new StringType(), nullable: false),
            new Field("SettlementCurrency", new StringType(), nullable: true),
            new Field("Status", new StringType(), nullable: false),
            new Field("Trader", new StringType(), nullable: true),
            new Field("Strategy", new StringType(), nullable: true),
            new Field("TradeDate", new Date32Type(), nullable: false),
            new Field("MaturityDate", new Date32Type(), nullable: true),
            new Field("ValueDate", new Date32Type(), nullable: true),
            new Field("BookingTimestamp", new TimestampType(TimeUnit.Millisecond, timezone: "UTC"), nullable: false),
            new Field("Notional", new DoubleType(), nullable: true),
            new Field("Quantity", new DoubleType(), nullable: true),
            new Field("StrikePrice", new DoubleType(), nullable: true),
            new Field("UnderlyingPrice", new DoubleType(), nullable: true)
        };

        return new Schema(fields, null);
    }

    public static Schema CreateCoreSchemaWithDictionaries()
    {
        var fields = new List<Field>
        {
            new Field("TradeId", new StringType(), nullable: false),
            new Field("ExternalRef", new StringType(), nullable: true),
            new Field("BookId", new DictionaryType(new Int16Type(), new Int32Type(), ordered: false), nullable: false),
            new Field("BookName", new DictionaryType(new Int16Type(), new StringType(), ordered: false), nullable: false),
            new Field("DeskId", new DictionaryType(new Int16Type(), new Int32Type(), ordered: false), nullable: false),
            new Field("DeskName", new DictionaryType(new Int16Type(), new StringType(), ordered: false), nullable: false),
            new Field("InstrumentType", new DictionaryType(new Int8Type(), new StringType(), ordered: false), nullable: false),
            new Field("InstrumentSubType", new DictionaryType(new Int8Type(), new StringType(), ordered: false), nullable: true),
            new Field("Counterparty", new DictionaryType(new Int16Type(), new StringType(), ordered: false), nullable: true),
            new Field("Currency", new DictionaryType(new Int8Type(), new StringType(), ordered: false), nullable: false),
            new Field("SettlementCurrency", new DictionaryType(new Int8Type(), new StringType(), ordered: false), nullable: true),
            new Field("Status", new DictionaryType(new Int8Type(), new StringType(), ordered: false), nullable: false),
            new Field("Trader", new DictionaryType(new Int16Type(), new StringType(), ordered: false), nullable: true),
            new Field("Strategy", new DictionaryType(new Int16Type(), new StringType(), ordered: false), nullable: true),
            new Field("TradeDate", new Date32Type(), nullable: false),
            new Field("MaturityDate", new Date32Type(), nullable: true),
            new Field("ValueDate", new Date32Type(), nullable: true),
            new Field("BookingTimestamp", new TimestampType(TimeUnit.Millisecond, timezone: "UTC"), nullable: false),
            new Field("Notional", new DoubleType(), nullable: true),
            new Field("Quantity", new DoubleType(), nullable: true),
            new Field("StrikePrice", new DoubleType(), nullable: true),
            new Field("UnderlyingPrice", new DoubleType(), nullable: true)
        };

        return new Schema(fields, null);
    }

    public static Schema CreateRiskSchema()
    {
        var coreFields = CreateCoreSchemaWithDictionaries().FieldsList.ToList();
        
        var riskFields = new List<Field>
        {
            new Field("Delta", new DoubleType(), nullable: true),
            new Field("DeltaContractUnits", new DoubleType(), nullable: true),
            new Field("DeltaLots", new DoubleType(), nullable: true),
            new Field("DeltaUSD", new DoubleType(), nullable: true),
            new Field("Gamma", new DoubleType(), nullable: true),
            new Field("GammaUSD", new DoubleType(), nullable: true),
            new Field("Vega", new DoubleType(), nullable: true),
            new Field("VegaUSD", new DoubleType(), nullable: true),
            new Field("Theta", new DoubleType(), nullable: true),
            new Field("ThetaUSD", new DoubleType(), nullable: true),
            new Field("Rho", new DoubleType(), nullable: true),
            new Field("RhoUSD", new DoubleType(), nullable: true),
            new Field("DV01", new DoubleType(), nullable: true),
            new Field("CS01", new DoubleType(), nullable: true),
            new Field("ConvexityAdjustment", new DoubleType(), nullable: true)
        };

        coreFields.AddRange(riskFields);
        return new Schema(coreFields, null);
    }

    public static Schema CreatePnLSchema()
    {
        var coreFields = CreateCoreSchemaWithDictionaries().FieldsList.ToList();
        
        var pnlFields = new List<Field>
        {
            new Field("TotalPnL", new DoubleType(), nullable: true),
            new Field("RealizedPnL", new DoubleType(), nullable: true),
            new Field("UnrealizedPnL", new DoubleType(), nullable: true),
            new Field("DailyPnL", new DoubleType(), nullable: true),
            new Field("MTDPnL", new DoubleType(), nullable: true),
            new Field("YTDPnL", new DoubleType(), nullable: true),
            new Field("NewTradeEffect", new DoubleType(), nullable: true),
            new Field("MarketMoveEffect", new DoubleType(), nullable: true),
            new Field("TimeDecayEffect", new DoubleType(), nullable: true),
            new Field("CashflowEffect", new DoubleType(), nullable: true),
            new Field("FundingEffect", new DoubleType(), nullable: true),
            new Field("ModelChangeEffect", new DoubleType(), nullable: true),
            new Field("TradeAmendEffect", new DoubleType(), nullable: true),
            new Field("Unexplained", new DoubleType(), nullable: true)
        };

        coreFields.AddRange(pnlFields);
        return new Schema(coreFields, null);
    }

    public static Schema CreatePAASchema()
    {
        var pnlFields = CreatePnLSchema().FieldsList.ToList();
        
        var commodityFields = new List<Field>
        {
            new Field("SpotPriceEffect", new DoubleType(), nullable: true),
            new Field("ForwardCurveEffect", new DoubleType(), nullable: true),
            new Field("BasisEffect", new DoubleType(), nullable: true),
            new Field("VolatilityEffect", new DoubleType(), nullable: true),
            new Field("CorrelationEffect", new DoubleType(), nullable: true),
            new Field("DividendEffect", new DoubleType(), nullable: true),
            new Field("InterestRateEffect", new DoubleType(), nullable: true),
            new Field("FXEffect", new DoubleType(), nullable: true),
            new Field("CarryEffect", new DoubleType(), nullable: true),
            new Field("RollEffect", new DoubleType(), nullable: true)
        };

        pnlFields.AddRange(commodityFields);
        return new Schema(pnlFields, null);
    }

    public static ArrowType GetIndexTypeForCardinality(int cardinality)
    {
        if (cardinality <= 127)
            return new Int8Type();
        if (cardinality <= 32767)
            return new Int16Type();
        return new Int32Type();
    }
}
