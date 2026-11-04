namespace BusOut.Core
{
    /// Các tiếng hiệu ứng của game (tên theo clip gốc trong Assets/_BusOut/Audio). AudioConfig gán clip cho từng tiếng.
    /// ⚠️ AudioConfig.asset lưu tiếng theo SỐ của enum: chỉ thêm vào cuối, không đổi thứ tự / xoá giữa chừng — đổi là clip
    /// lệch sang tiếng khác mà không báo lỗi gì.
    public enum SoundId
    {
        TapButton,
        HitCar,
        CarMoveToBusStop,
        CarLeaveBusStop,
        PassengerGetInCar,
        WinGame,
        LoseGame,
        Coin,
        BonusCoin,
        Helicopter,
    }
}
