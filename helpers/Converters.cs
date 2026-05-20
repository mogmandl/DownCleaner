using System.Globalization;
using System.Windows.Data;

namespace FileCleaner.Helpers;

public class BoolToInUseConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? "사용 중" : "";

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}
