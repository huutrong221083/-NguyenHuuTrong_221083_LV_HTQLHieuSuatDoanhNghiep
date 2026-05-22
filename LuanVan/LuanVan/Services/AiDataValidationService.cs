namespace LuanVan.Services;

public interface IAiDataValidationService
{
    IReadOnlyList<string> ValidateTaskDelayInput(AiTaskDelayModelInput input);
    IReadOnlyList<string> ValidatePerformanceInput(AiPerformanceModelInput input);
}

public sealed class AiDataValidationService : IAiDataValidationService
{
    public IReadOnlyList<string> ValidateTaskDelayInput(AiTaskDelayModelInput input)
    {
        var errors = new List<string>();

        if (input.DoKho <= 0)
        {
            errors.Add("DoKho phai lon hon 0.");
        }

        if (input.DoUuTien <= 0)
        {
            errors.Add("DoUuTien phai lon hon 0.");
        }

        if (input.SoNguoiThamGia <= 0)
        {
            errors.Add("SoNguoiThamGia phai lon hon 0.");
        }

        if (input.TienDoHienTai is < 0 or > 100)
        {
            errors.Add("TienDoHienTai phai trong khoang 0..100.");
        }

        if (input.SoNgayConLai < -3650)
        {
            errors.Add("SoNgayConLai khong hop le.");
        }

        return errors;
    }

    public IReadOnlyList<string> ValidatePerformanceInput(AiPerformanceModelInput input)
    {
        var errors = new List<string>();

        if (input.SoCongViecHoanThanh < 0)
        {
            errors.Add("SoCongViecHoanThanh khong duoc am.");
        }

        if (input.SoCongViecTreHan < 0)
        {
            errors.Add("SoCongViecTreHan khong duoc am.");
        }

        if (input.ThoiGianTrungBinh < 0)
        {
            errors.Add("ThoiGianTrungBinh khong duoc am.");
        }

        if (input.KpiTrungBinh is < 0 or > 100)
        {
            errors.Add("KpiTrungBinh phai trong khoang 0..100.");
        }

        return errors;
    }
}
