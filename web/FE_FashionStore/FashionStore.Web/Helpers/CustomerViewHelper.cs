using System;
using System.Collections.Generic;
namespace FashionStore.Web.Helpers {
 public static class CustomerViewHelper {
  private static readonly IDictionary<string,string> Labels=new Dictionary<string,string>{
   {"CAN_HO_TRO","Cần liên hệ cửa hàng"},{"CHUA_XAC_MINH","Chưa xác minh"},
   {"Vừa xác nhận đơn","Vừa xác nhận đơn"},{"Shipper đã lấy hàng","Shipper đã lấy hàng"},
   {"Hàng đang được vận chuyển","Hàng đang được vận chuyển"},{"Hàng giao thành công","Hàng giao thành công"},
   {"Cần liên hệ cửa hàng","Cần liên hệ cửa hàng"},{"MOI","Đơn mới"},{"XAC_NHAN","Đã xác nhận"},{"DANG_XU_LY","Đang xử lý"},{"HOAN_TAT","Hoàn thành"},{"HUY","Đã hủy"},
   {"CHO_XAC_NHAN","Chờ xác nhận"},{"CHUA_GIAO","Chưa giao"},{"DANG_CHUAN_BI","Đang chuẩn bị"},{"DANG_GIAO","Đang giao"},{"DA_GIAO","Đã giao"},{"HOAN","Hoàn trả"},
   {"CHUA_THANH_TOAN","Chưa thanh toán"},{"DA_THANH_TOAN","Đã thanh toán"},{"HOAN_TIEN","Đã hoàn tiền"},{"HOAN_MOT_PHAN","Hoàn tiền một phần"},{"THAT_BAI","Không thành công"},
   {"CHO_DUYET","Chờ duyệt"},{"DA_DUYET","Đã duyệt"},{"TU_CHOI","Từ chối"},{"TICH","Tích điểm"},{"SU_DUNG","Sử dụng điểm"},{"DIEU_CHINH","Điều chỉnh"},
   {"TRA","Trả hàng"},{"DOI","Đổi hàng"},{"DOI_VA_TRA","Đổi và trả"},{"DON_HANG","Đơn hàng"},{"GIAO_HANG","Giao hàng"},{"THANH_TOAN","Thanh toán"}};
  public static string Status(string value){string text;return value!=null&&Labels.TryGetValue(value,out text)?text:"Đang cập nhật";}
 }
}
