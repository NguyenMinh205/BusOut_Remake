# Traffic Jam Out

## 📖 Giới thiệu (Introduction)
**Traffic Jam Out** là một tựa game giải đố gỡ kẹt xe (Parking Jam / Bus Jam) 3D trên mobile, phát triển bằng Unity. Bãi đỗ chật kín xe nhiều màu, hành khách xếp hàng chờ ở bến: người chơi chạm vào xe để đưa nó thoát khỏi bãi, chạy lên bến đón đúng khách cùng màu. Mỗi ván ngắn 1–3 phút, độ khó tăng dần qua 100 level, mỗi 4 level mở thêm một cơ chế mới.

Đây là dự án cá nhân để học tập và làm portfolio, lấy cảm hứng từ thể loại "jam puzzle" trên mobile.

---

## 🎮 Các tính năng chính (Main Features)
- **100 level tự thiết kế**: sinh bằng bộ sinh level riêng và cân bằng độ khó bằng mô phỏng (bot tự chơi), mọi level đều **giải được mà không cần booster**.
- **5 cơ chế đặc biệt** mở dần (level 8 → 24): Bãi chờ, Xe ẩn màu, Khoá & Chìa, Khách ẩn màu, Ô công trường. Mỗi cơ chế có màn giới thiệu riêng lần đầu gặp.
- **4 booster**: Shuffle, Sort, Undo, Helicopter. Mỗi booster có bài hướng dẫn chơi thử ở level mở khoá.
- **Hướng dẫn cho người mới**: bàn tay chỉ từng bước ở level 1, người chơi mới vào thẳng ván không qua Menu.
- **Menu bản đồ 3D**: bản đồ đường đi qua 10 chủ đề, xe chạy sang bệ level mới sau mỗi chiến thắng, trời và cảnh đổi theo chủ đề.
- **Game feel**: xe "bung" vào bãi, va chạm có rung camera và rung máy (haptic), xe đủ khách nảy lên kèm hiệu ứng, chữ khen combo (GOOD! → EXCELLENT!), coin bay về thanh trên, pháo giấy khi thắng.
- **Meta & kiếm tiền**: quà đăng nhập 7 ngày, hồi sinh và mở thêm ô bằng coin hoặc quảng cáo, nhận thưởng x2 bằng quảng cáo, mua "Remove Ads" (giả lập). Quảng cáo AdMob dùng ID thử nghiệm.
- **Âm thanh & cài đặt**: nhạc nền, tiếng hiệu ứng, bật/tắt nhạc, tiếng, rung.

---

## 🚌 Cơ chế Gameplay (Gameplay Mechanics)

### 1. Vòng chơi cốt lõi
- Mỗi xe có **mũi tên chỉ hướng** chạy. Chạm vào xe: nếu đường phía trước trống, xe chạy ra làn và vào một ô trống ở bến; nếu bị xe khác chặn, xe lao tới, đâm vào xe chặn rồi lùi về chỗ cũ.
- Bến có **4 ô đỗ** mở sẵn (tối đa 7). Hàng khách nhiều màu đứng chờ; người đầu hàng lên xe **cùng màu** đang đỗ ở bến. Xe đủ ghế (4 / 6 / 10 chỗ tuỳ loại xe) thì rời bến, nhường ô.
- **Thắng** khi đưa hết khách lên xe. **Thua** khi bến hết ô trống và không còn xe nào ở bến nhận được người đầu hàng.
- Thứ tự màu khách được **lên kế hoạch theo đồ thị chặn** giữa các xe, nên ván nào cũng có lời giải.

### 2. Các cơ chế đặc biệt
- 🏠 **Bãi chờ (Garage)** – Level 8: nhiều xe xếp trong một cổng, xe phía trước rời đi thì xe kế lăn ra đứng đúng chỗ đó.
- ❓ **Xe ẩn màu (Mystery Car)** – Level 12: màu xe bị che, chỉ lộ ra khi không còn xe nào chặn đường.
- 🔒 **Khoá & Chìa (Lock & Key)** – Level 16: xe bị khoá không chạy được cho tới khi xe mang chìa cùng số rời bãi.
- 👥 **Khách ẩn màu (Mystery Passengers)** – Level 20: một số khách giấu màu, chỉ lộ khi tiến gần đầu hàng.
- 🚧 **Ô công trường (Construction)** – Level 24: một ô ở bến bị rào, mở ra sau khi đủ số khách lên xe.

### 3. Booster
- 🔀 **Shuffle** (Level 2): xáo lại màu hàng khách để mở nước đi mới.
- 📋 **Sort** (Level 3): sắp lại khách để mọi xe đang ở bến đều đủ khách và rời đi.
- ↩️ **Undo** (Level 4): đưa chiếc xe vừa gửi lên bến về lại chỗ đỗ cũ.
- 🚁 **Helicopter** (Level 5): nhấc bất kỳ xe nào (kể cả xe đang bị chặn) bay thẳng lên bến.

Hết lượt booster thì mua thêm bằng coin hoặc xem quảng cáo.

### 4. Combo & Hồi sinh
- Nhiều xe đủ khách **liên tiếp trong vài giây** sẽ kích hoạt chữ khen combo: GOOD! → NICE! → GREAT! → EXCELLENT!.
- Khi thua, popup hồi sinh hiện **tiến độ ván** ("Only N passengers left!"): trả coin hoặc xem quảng cáo để mở thêm 1 ô và chơi tiếp.

---

## 🚀 Hướng dẫn cách chơi (How to play)
1. **Quan sát bãi xe**: xem mũi tên trên từng xe để biết xe chạy hướng nào và xe nào đang bị chặn.
2. **Chạm xe để gửi lên bến**: ưu tiên xe **cùng màu với khách đứng đầu hàng**, khách sẽ lên xe ngay.
3. **Giữ ô trống ở bến**: gửi xe sai màu sẽ chiếm ô; hết ô trống là thua. Hãy tính trước thứ tự gỡ xe.
4. **Dùng booster khi bí**: Undo để sửa nước vừa đi, Helicopter để giải cứu xe bị kẹt sâu, Sort / Shuffle để đổi lại hàng khách.
5. **Hoàn thành level**: đưa hết khách lên xe để nhận coin (x2 nếu xem quảng cáo), xe trên bản đồ chạy sang level kế tiếp.

---

## 🛠️ Công cụ & Công nghệ sử dụng (Tech Stack)
- **Game Engine**: Unity 6 (6000.3), Universal Render Pipeline (URP), C#.
- **Thư viện**: DOTween (hoạt ảnh), TextMeshPro, Input System, UI Particle (hiệu ứng hạt trên UI), Google Mobile Ads + UMP (quảng cáo, đồng thuận), CandyCoded HapticFeedback (rung).
- **Design Pattern**: MVP một chiều (Model – View – Presenter), Singleton, Observer (EventBus dạng struct, không cấp phát rác), Object Pooling, ScriptableObject Config (mọi con số chỉnh được nằm trong config, không rải trong code).

### Kiến trúc & điểm kỹ thuật
- **Chia module bằng Assembly Definition**: `DesignPattern` → `Core` → `Gameplay` / `Menu` / `Ads`. Các module chỉ tham chiếu Core và nói chuyện với nhau qua sự kiện.
- **Đồ thị chặn (Blocking Graph)**: dò chặn bằng tia vật lý, dựng đồ thị "xe nào chặn xe nào". Mọi cơ chế (bãi chờ, khoá) được biểu diễn thành cạnh của đồ thị này, nên bộ lên kế hoạch màu khách, booster và hướng dẫn đều dùng chung một nguồn sự thật.
- **Bộ lên kế hoạch màu khách**: sinh thứ tự màu hành khách theo các xe chạy được, có trộn nhóm và "điểm khó" để tạo đỉnh áp lực mà vẫn luôn giải được.
- **Bộ sinh level + Level Builder (Editor Tool)**: đặt xe theo thứ tự thoát ngược nên không bao giờ kẹt vòng. Có 4 mức độ khó và tuỳ chọn từng cơ chế. Cửa sổ Level Builder cho phép dựng và sửa level bằng tay.
- **Cân bằng bằng mô phỏng**: script Python mô phỏng bàn chơi với nhiều bot (tham lam theo màu, có booster, biết kế hoạch). Mỗi level sinh 20 ứng viên, đo tỉ lệ thắng rồi chọn bản rơi vào dải độ khó mục tiêu (99/100 level trúng dải).
- **Save mã hoá + giờ mạng**: dữ liệu lưu mã hoá, có phiên bản save. Quà ngày dùng giờ từ máy chủ (header HTTP Date) để chặn chỉnh giờ máy.
- **Tối ưu cho mobile**: pool xe và khách tạo sẵn dưới màn Loading, shader viết lại cho SRP Batcher, GPU Instancing cho thân xe, không cấp phát bộ nhớ mỗi khung hình trong vòng lặp chính.

---

## ⚠️ Lưu ý
Dự án phục vụ học tập và portfolio, không phát hành thương mại. Một phần asset 3D, âm thanh và bản đồ Menu vẫn đang là asset tạm và sẽ được thay dần bằng asset riêng.
