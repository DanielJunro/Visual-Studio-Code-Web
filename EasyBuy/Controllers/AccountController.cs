using EasyBuy.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;

namespace EasyBuy.Controllers
{
    public class AccountController : Controller
    {
        private readonly EasyBuyContext _context;
        private EasyBuy.Method.Method method = new EasyBuy.Method.Method();
        public AccountController(EasyBuyContext context)
        {
            _context = context;
        }
        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetInt32("UserID") != null)
            {
                return RedirectToAction("TrangChu", "Home");
            }
            return View();
        }

        [HttpPost]
        public IActionResult Login(string name, string password)
        {
            try
            {
                bool isEmail = name.Contains("@");
                var user = isEmail
                    ? _context.Users.FirstOrDefault(u => u.Email == name)
                    : _context.Users.FirstOrDefault(u => u.Phone == name);

                if (user == null)
                {
                    ViewBag.Error = "Tài khoản không tồn tại";
                    return View();
                }

                if (user.AccountStatus == "Locked" && user.LockedAt != null)
                {
                    var minutesLocked = (DateTime.Now - user.LockedAt.Value).TotalMinutes;
                    if (minutesLocked >= 15)
                    {
                        user.AccountStatus = "Active";
                        user.FailedLoginCount = 0;
                        user.LockedAt = null;
                        _context.SaveChanges();
                    }
                    else
                    {
                        ViewBag.Error = $"Tài khoản bị khóa. Vui lòng thử lại sau {Math.Ceiling(15 - minutesLocked)} phút.";
                        return View();
                    }
                }
                else if (user.AccountStatus == "Locked")
                {
                    ViewBag.Error = "Tài khoản đã bị khóa.";
                    return View();
                }

                if (BCrypt.Net.BCrypt.Verify(password, user.Password))
                {
                    // Đăng nhập thành công
                    HttpContext.Session.Clear();
                    HttpContext.Session.SetInt32("UserID", user.UserId);
                    HttpContext.Session.SetString("Phone", user.Phone);
                    HttpContext.Session.SetString("Role", user.Role);

                    // Reset failed login
                    user.FailedLoginCount = 0;
                    _context.SaveChanges();

                    var log = new LogActivity
                    {
                        UserId = HttpContext.Session.GetInt32("UserID"),
                        Action = "Đăng nhập",
                        Timestamp = DateTime.Now,
                    };
                    _context.Add(log);
                    _context.SaveChanges();
                    return RedirectToAction("TrangChu", "Home");
                }
                else
                {
                    user.FailedLoginCount++;

                    if (user.FailedLoginCount >= 3)
                    {
                        user.AccountStatus = "Locked";
                        user.LockedAt = DateTime.Now;
                        ViewBag.Error = "Tài khoản đã bị khóa do đăng nhập sai nhiều lần.";
                    }
                    else
                    {
                        ViewBag.Error = "Mật khẩu không chính xác.";
                    }

                    _context.SaveChanges();
                    return View();
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Đã xảy ra lỗi trong quá trình đăng nhập: " + ex.Message;
                return View();
            }
        }


        [HttpPost]
        public IActionResult Logout()
        {
            try
            {
                // Xóa session
                HttpContext.Session.Clear();

                // Xóa cookie session (tên cookie có thể khác, tùy cấu hình)
                Response.Cookies.Delete(".AspNetCore.Session");

                // Xóa cookie xác thực (nếu dùng Identity hoặc cookie auth)
                Response.Cookies.Delete(".AspNetCore.Cookies");

                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                // Hiển thị lỗi hoặc xử lý khác
                TempData["Error"] = "Đăng xuất thất bại: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }


        [HttpGet]
        public IActionResult Register()
        {

            return View();
        }
        [HttpPost]
        public IActionResult Register(string phone, string password, string repassword, string name, string email)
        {
            try
            {
                if (method.IsEmpty(phone) || method.IsEmpty(password) || method.IsEmpty(repassword) || method.IsEmpty(name) || method.IsEmpty(email))
                {
                    ViewBag.MS = "Các trường không được để trống";
                    return View();
                }

                if (!method.IsValidPassword(password))
                {
                    ViewBag.MS = "Mật khẩu phải lớn hơn 8 ký tự và có chữ hoa chữ thường";
                    return View();
                }

                if (!method.IsValidVietnamPhoneNumber(phone))
                {
                    ViewBag.MS = "Số điện thoại không hợp lệ";
                    return View();
                }

                if (password != repassword)
                {
                    ViewBag.MS = "Mật khẩu nhập lại không đúng";
                    return View();
                }

                if (!method.IsValidName(name))
                {
                    ViewBag.MS = "Tên không được chứa số hay ký tự đặc biệt";
                    return View();
                }

                if (!method.IsValidEmail(email))
                {
                    ViewBag.MS = "Email không hợp lệ";
                    return View();
                }

                if (_context.Users.Any(u => u.Phone == phone))
                {
                    ViewBag.MS = "Số điện thoại đã có người sử dụng";
                    return View();
                }

                if (_context.Users.Any(u => u.Email == email))
                {
                    ViewBag.MS = "Email đã có người sử dụng";
                    return View();
                }

                var user = new User
                {
                    Phone = phone,
                    Email = email,
                    Password = BCrypt.Net.BCrypt.HashPassword(password),
                    FullName = name,
                    FailedLoginCount = 0,
                    AccountStatus = "Active"
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                // ✅ Tự động đăng nhập tài khoản mới tạo
                HttpContext.Session.Clear();
                HttpContext.Session.SetInt32("UserID", user.UserId);
                HttpContext.Session.SetString("Phone", user.Phone);
                HttpContext.Session.SetString("Role", user.Role ?? "User");

                // Lưu lại log đăng nhập
                var log = new LogActivity
                {
                    UserId = user.UserId,
                    Action = "Tự động đăng nhập sau khi đăng ký",
                    Timestamp = DateTime.Now,
                };
                _context.Add(log);
                _context.SaveChanges();

                TempData["Success"] = "Tạo tài khoản thành công và đã đăng nhập!";
                return RedirectToAction("TrangChu", "Home");
            }
            catch (Exception ex)
            {
                ViewBag.MS = "Đã xảy ra lỗi hệ thống: " + ex.Message;
                return View();
            }
        }


        [HttpGet]
        public IActionResult UpdateAccount()
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpPost]
        public IActionResult UpdateAccount(string name, string email, string phone, string password)
        {
            try
            {
                int? userID = HttpContext.Session.GetInt32("UserID");
                var user = _context.Users.Find(userID);

                if (method.IsEmpty(password))
                {
                    ViewBag.MS = "Bạn phải nhập mật khẩu để xác nhận thay đổi.";
                    return View(user);
                }
                bool isValidPassword = BCrypt.Net.BCrypt.Verify(password, user.Password);
                if (!isValidPassword)
                {
                    ViewBag.MS = "Mật khẩu không đúng.";
                    return View(user);
                }

                // Chỉ cập nhật trường nào được nhập
                if (!method.IsEmpty(name))
                {
                    user.FullName = name;
                }
                if (!method.IsEmpty(email))
                {
                    if (!method.IsValidEmail(email))
                    {
                        ViewBag.MS = "Email không hợp lệ";
                        return View(user);
                    }
                    if (_context.Users.Any(u => u.Email == email && u.UserId != user.UserId))
                    {
                        ViewBag.MS = "Email đã có người sử dụng";
                        return View(user);
                    }
                    user.Email = email;
                }
                if (!method.IsEmpty(phone))
                {
                    if (!method.IsValidVietnamPhoneNumber(phone))
                    {
                        ViewBag.MS = "Số điện thoại không hợp lệ";
                        return View(user);
                    }
                    if (_context.Users.Any(u => u.Phone == phone && u.UserId != user.UserId))
                    {
                        ViewBag.MS = "Số điện thoại đã có người sử dụng";
                        return View(user);
                    }
                    user.Phone = phone;
                }

                _context.SaveChanges();

                ViewBag.Success = "Cập nhật tài khoản thành công!";
                return View(user);
            }
            catch (Exception ex)
            {
                ViewBag.MS = "Có lỗi hệ thống";
                return View();
            }
        }


        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpPost]
        public IActionResult ChangePassword(string newpassword, string renewpassword, string password)
        {
            try
            {
                int? userID = HttpContext.Session.GetInt32("UserID");
                if (userID == null)
                {
                    return RedirectToAction("Login", "Account");
                }
                var user = _context.Users.Find(userID);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                if (method.IsEmpty(newpassword) || method.IsEmpty(renewpassword) || method.IsEmpty(password))
                {
                    ViewBag.MS = "Các trường không được để trống";
                    return View(user);
                }
                bool isValidPassword = BCrypt.Net.BCrypt.Verify(password, user.Password);
                if (!isValidPassword)
                {
                    ViewBag.MS = "Mật khẩu hiện tại không đúng.";
                    return View(user);
                }
                if (newpassword != renewpassword)
                {
                    ViewBag.MS = "Mật khẩu nhập lại không đúng";
                    return View(user);
                }
                if (!method.IsValidPassword(newpassword))
                {
                    ViewBag.MS = "Mật khẩu phải lớn hơn 8 ký tự và có chữ hoa chữ thường";
                    return View(user);
                }
                user.Password = BCrypt.Net.BCrypt.HashPassword(newpassword);
                _context.SaveChanges();
                ViewBag.Success = "Đổi mật khẩu thành công!";
                return View(user);
            }
            catch (Exception ex)
            {
                ViewBag.MS = "Lỗi hệ thống: " + ex.Message;
                return View();
            }
        }

        [HttpGet]
        public IActionResult ListAdress()
        {
            int? userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var addresses = _context.Addresses
                .Where(a => a.UserId == userID.Value)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            return View(addresses);
        }
        [HttpGet]
        public IActionResult AddAdress()
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
            {
                return RedirectToAction("Login", "Account");
            }
            return View();
        }

        [HttpPost]
        public IActionResult AddAdress(string city, string district, string ward, string street, string phone)
        {
            try
            {
                int? userID = HttpContext.Session.GetInt32("UserID");
                var user = _context.Users.Find(userID);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Tạo model để giữ lại dữ liệu khi có lỗi
                var model = new Address
                {
                    City = city,
                    District = district,
                    Ward = ward,
                    Street = street,
                    Phone = phone
                };

                if (method.IsEmpty(city) || method.IsEmpty(district) || method.IsEmpty(ward) || method.IsEmpty(street) || method.IsEmpty(phone))
                {
                    ViewBag.MS = "Các trường không được bỏ trống";
                    return View(model);
                }

                if (!method.IsValidVietnamPhoneNumber(phone))
                {
                    ViewBag.MS = "Số điện thoại không hợp lệ";
                    return View(model);
                }

                var adress = new Address
                {
                    City = city,
                    District = district,
                    Ward = ward,
                    Street = street,
                    Phone = phone,
                    CreatedAt = DateTime.Now,
                    UserId = userID.Value
                };
                _context.Add(adress);
                _context.SaveChanges();
                TempData["Success"] = "Thêm địa chỉ thành công!";
                return RedirectToAction("ListAdress");
            }
            catch (Exception ex)
            {
                ViewBag.MS = "Lỗi hệ thống: " + ex.Message;
                // Trả lại dữ liệu đã nhập
                var model = new Address
                {
                    City = city,
                    District = district,
                    Ward = ward,
                    Street = street,
                    Phone = phone
                };
                return View(model);
            }
        }
        [HttpPost]
        public IActionResult DeleteAddress(int id)
        {
            try
            {
                int? userID = HttpContext.Session.GetInt32("UserID");
                if (userID == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var address = _context.Addresses.FirstOrDefault(a => a.AddressId == id && a.UserId == userID.Value);
                if (address == null)
                {
                    TempData["MS"] = "Không tìm thấy địa chỉ hoặc bạn không có quyền xóa.";
                    return RedirectToAction("ListAdress");
                }

                _context.Addresses.Remove(address);
                _context.SaveChanges();
                TempData["Success"] = "Xóa địa chỉ thành công!";
                return RedirectToAction("ListAdress");
            }
            catch
            {
                TempData["MS"] = "Không thể xóa địa chỉ này";
                return RedirectToAction("ListAdress");
            }
        }
        [HttpGet]
        public IActionResult UpdateAddress(int id)
        {
            int? userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var address = _context.Addresses.FirstOrDefault(a => a.AddressId == id && a.UserId == userID.Value);
            if (address == null)
            {
                TempData["MS"] = "Không tìm thấy địa chỉ hoặc bạn không có quyền sửa.";
                return RedirectToAction("ListAdress");
            }

            return View(address);
        }

        [HttpPost]
        public IActionResult UpdateAddress(int id, string city, string district, string ward, string street, string phone)
        {
            try
            {
                int? userID = HttpContext.Session.GetInt32("UserID");
                if (userID == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var address = _context.Addresses.FirstOrDefault(a => a.AddressId == id && a.UserId == userID.Value);
                if (address == null)
                {
                    TempData["MS"] = "Không tìm thấy địa chỉ hoặc bạn không có quyền sửa.";
                    return RedirectToAction("ListAdress");
                }

                if (method.IsEmpty(city) || method.IsEmpty(district) || method.IsEmpty(ward) || method.IsEmpty(street) || method.IsEmpty(phone))
                {
                    ViewBag.MS = "Các trường không được bỏ trống";
                    return View(address);
                }

                if (!method.IsValidVietnamPhoneNumber(phone))
                {
                    ViewBag.MS = "Số điện thoại không hợp lệ";
                    return View(address);
                }

                // Cập nhật thông tin
                address.City = city;
                address.District = district;
                address.Ward = ward;
                address.Street = street;
                address.Phone = phone;

                _context.SaveChanges();
                TempData["Success"] = "Cập nhật địa chỉ thành công!";
                return RedirectToAction("ListAdress");
            }
            catch (Exception ex)
            {
                TempData["MS"] = "Lỗi hệ thống: " + ex.Message;
                return RedirectToAction("ListAdress");
            }
        }

        [HttpPost]
        public IActionResult LockedAccount(bool confirm)
        {
            int? userid = HttpContext.Session.GetInt32("UserID");
            var user = _context.Users.Find(userid);
            if (user == null)
            {
                TempData["MS"] = "Không tìm thấy tài khoản.";
                return RedirectToAction("Login", "Account");
            }

            if (confirm)
            {
                user.AccountStatus = "Locked";
                Logout();
                _context.SaveChanges();
                TempData["Success"] = "Khóa tài khoản thành công!";
            }
            else
            {
                TempData["MS"] = "Bạn đã hủy thao tác khóa tài khoản.";
            }
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult DeleteAccount()
        {
            int? userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = _context.Users.Find(userID);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View(user);
        }

        [HttpPost]
        public IActionResult DeleteAccount(string password, bool confirm)
        {
            try
            {
                int? userID = HttpContext.Session.GetInt32("UserID");
                if (userID == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                var user = _context.Users.Find(userID);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                if (!confirm)
                {
                    ViewBag.MS = "Bạn phải xác nhận để xóa tài khoản.";
                    return View(user);
                }

                if (method.IsEmpty(password))
                {
                    ViewBag.MS = "Bạn phải nhập mật khẩu để xác nhận xóa tài khoản.";
                    return View(user);
                }

                bool isValidPassword = BCrypt.Net.BCrypt.Verify(password, user.Password);
                if (!isValidPassword)
                {
                    ViewBag.MS = "Mật khẩu không đúng.";
                    return View(user);
                }

                // Xóa các dữ liệu liên quan trước khi xóa user
                // Xóa địa chỉ
                var addresses = _context.Addresses.Where(a => a.UserId == userID.Value).ToList();
                if (addresses.Any())
                {
                    _context.Addresses.RemoveRange(addresses);
                }

                // Xóa log activities
                var logActivities = _context.LogActivities.Where(l => l.UserId == userID.Value).ToList();
                if (logActivities.Any())
                {
                    _context.LogActivities.RemoveRange(logActivities);
                }

                // Cuối cùng xóa user
                _context.Users.Remove(user);
                _context.SaveChanges();

                // Xóa session và cookie
                HttpContext.Session.Clear();
                Response.Cookies.Delete(".AspNetCore.Session");
                Response.Cookies.Delete(".AspNetCore.Cookies");

                // Đảm bảo không còn session tồn tại
                HttpContext.Session.Remove("UserID");
                HttpContext.Session.Remove("Phone");
                HttpContext.Session.Remove("Role");

                // Gửi người dùng đến trang đăng ký
                TempData["Success"] = "Tài khoản đã được xóa thành công!";
                return RedirectToAction("Register", "Account");
            }
            catch (Exception ex)
            {
                ViewBag.MS = "Có lỗi xảy ra khi xóa tài khoản: " + ex.Message;
                var user = _context.Users.Find(HttpContext.Session.GetInt32("UserID"));
                return View(user);
            }
        }
    }
}