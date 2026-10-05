# TungMEP2 PrintSheets

Add-in độc lập cho Autodesk Revit 2024, chỉ chứa chức năng PrintSheets.

## Bản hiện tại — 2026.10.03.1 (03/10/2026)

- Bộ cài mới: `dist/2026.10.03.1/TungMEP2-PrintSheets-Setup-2026.10.03.1-Revit2024.exe`.
- Portable: `dist/2026.10.03.1/TungMEP2-PrintSheets-Portable-2026.10.03.1-Revit2024.zip`.
- Source đang dùng: `maintenance/2026.10.03.1/src`. Lệnh build ở root chuyển
  sang source này; thư mục `src` cũ ở root chỉ còn là nguồn lịch sử tháng 8.
- Giữ giao diện PDF # / Nguồn sheet / STT, di chuyển nhóm và sheet, tạo STT nháp
  và ghi STT riêng. Export không tự sửa STT. In modal, không in nền/modeless.
- Sheet trùng chỉ được tích tại một nhóm; chọn nhóm khác tự bỏ tích dòng cũ.
  Chọn tất cả và export chỉ lấy mỗi sheet một lần, theo thứ tự đang hiển thị.
- Hỗ trợ đường dẫn đích dài bằng Unicode Win32 I/O cho PDF/DWG và đổi tên PDF;
  Revit vẫn xuất vào Temp local ngắn. Không đổi registry/cấu hình Revit.
- Có kiểm tra file đích, bảo toàn file cũ khi chuyển thất bại; dọn Temp sau job
  hoàn tất bình thường. Crash vẫn có thể để lại job Temp chưa hoàn tất.
- Bộ cài cũ không ghi phiên bản đã chuyển vào `dist/archive/pre-2026.10.03.1/`.
- Giới hạn Windows/từng thành phần đường dẫn, quyền ghi và máy chủ mạng vẫn áp dụng.
- Tiêu đề hộp thoại và metadata cho biết đúng phiên bản. Revit cần khởi động lại.

## Lịch sử rollback — 2026.07.31.1-STT

Bộ dùng cho yêu cầu quay về giao diện có STT nằm tại
`dist/2026.07.31.1-STT/TungMEP2-PrintSheets/` (chạy CMD cài đặt), hoặc ZIP
`dist/TungMEP2-PrintSheets-2026.07.31.1-STT-Revit2024.zip`.

- Engine lấy từ backup build 01/08/2026, vẫn mang nhãn phiên bản `2026.07.31.1`.
- SHA-256: `3A854A3351EE2FF00BC9CFFE79CB0E52DFF08FD541023AFDA29433827A866599`.
- Có cột PDF #, Nguồn sheet, STT; nhóm nguồn riêng và di chuyển nhóm/sheet.
- Tạo STT nháp rồi bấm Ghi STT vào Sheet. Export giữ thứ tự PDF #, không tự sửa STT.
- In modal, không có chế độ vừa in vừa dựng hình.
- DLL gốc 31/07 (hash `7A10E084...`) không có đầy đủ các mục STT này.

Source/build và EXE tháng 8 bên dưới được giữ để tham khảo; **không chạy build
hoặc dùng EXE đó để cài bản rollback STT**. Source tương ứng của bộ rollback
được giữ nguyên tại `backups/before-responsive-print-20260813-083735/src`.

## Thông tin bản tháng 8 lưu để tham khảo (không phải bản hiện tại)

- Assembly riêng: `TungMEP2.PrintSheets.dll`.
- Entry point riêng: `TungMEP2.PrintSheets.Application`.
- AddInId và manifest riêng.
- Ribbon tab riêng: `TungMEP2 Print`.
- Không tham chiếu `TungMEP2.dll`, `VP_RevitAPI.dll` hoặc Nice3point.
- State riêng tại `%LOCALAPPDATA%\TungMEP2-PrintSheets\State`.
- Có thể cài song song với bản TungMEP2 đầy đủ.
- Nguồn sheet có thể là Revit Print Set hoặc Sheet List Schedule; schedule chỉ lấy
  các sheet đang xuất hiện sau khi áp dụng filter.
- Tự kiểm tra STT trước khi xuất; STT thiếu, sai, trùng, đảo thứ tự hoặc không
  liền mạch sẽ được đánh lại `1…n` theo Sheet Number.
- Có Revit Native PDF 300/600 DPI, chế độ Font-safe Raster và các máy in PDF ảo
  được Windows/Revit nhận diện.
- `PDF-XChange Lite` là máy in mặc định; nếu máy chưa cài driver này thì tool
  tự fallback sang Revit Native PDF.
- PDF và DWG có đường dẫn riêng; hỗ trợ combine PDF, DWG Export Setup, ghi đè
  an toàn, tạm dừng và ngưng xuất.
- Cửa sổ PrintSheets chạy modeless. Tool chỉ giao cho Revit tối đa một sheet
  trong mỗi nhịp, sau đó nhường tối thiểu 750 ms để người dùng đổi view và tiếp
  tục dựng hình. Trong vài giây Revit đang xử lý chính sheet hiện tại, giao diện
  vẫn có thể tạm đứng ngắn do API Export/Print của Revit là đồng bộ.
- Danh sách sheet, thứ tự, đường dẫn và setup được chốt khi bấm xuất. Nếu chuyển
  sang model khác, job tự chờ; quay lại đúng model thì tiếp tục. Không đóng model
  nguồn khi job chưa kết thúc.
- Pause và Stop có hiệu lực tại ranh giới giữa hai sheet. Phần chờ PDF-XChange
  ghi xong file và phần gộp PDF chạy nền, không gọi Revit API.
- Combine xuất các PDF sheet vào một thư mục tạm riêng, gộp theo đúng thứ tự rồi
  mới thay file đích. Nếu dừng, PDF hỏng hoặc gộp lỗi, file combine cũ vẫn được giữ.
- Khi combine bằng PDF-XChange Lite, tool chờ file có `%%EOF` và ổn định trước
  khi chuyển file hoặc hiện thông báo hoàn tất.
- Trong lúc xuất, tool tạm tắt `Show Save As` và `Run App` của PDF-XChange Lite,
  sau đó khôi phục đúng cấu hình cũ; đường dẫn đã nhập trong PrintSheets được
  dùng mà không hỏi lại.

## Build và kiểm tra

```powershell
.\Build-TungMEP2-PrintSheets.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-Standalone.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-ResponsivePrint.ps1
```

## Cài đặt

File cài gọn nhất để chuyển sang máy khác:

```text
dist\TungMEP2-PrintSheets-Setup-Revit2024.exe
```

Đây là một file EXE duy nhất, cài theo user, không cần quyền Administrator và tự tạo
manifest Revit bằng đường dẫn của máy đích.

Portable ZIP dự phòng:

```text
dist\TungMEP2-PrintSheets-Portable-Revit2024.zip
```

Bộ cài CMD truyền thống:

```text
release\TungMEP2-PrintSheets\Install-TungMEP2-PrintSheets.cmd
```

Gỡ riêng add-in này bằng:

```text
release\TungMEP2-PrintSheets\Uninstall-TungMEP2-PrintSheets.cmd
```

Quá trình gỡ không tác động tới TungMEP2 đầy đủ.
