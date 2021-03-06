﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Ator.Site.Models;
using Ator.Repository;
using Ator.Utility.Ext;
using Ator.Model;
using Microsoft.Extensions.Logging;
using Ator.IService;
using Ator.Site.Attribute;
using Ator.Entity.Sys;
using Microsoft.EntityFrameworkCore;
using LinqKit;

namespace Ator.Site.Controllers
{
    [Layout]
    public class HomeController : BaseController
    {
        private UnitOfWork _unitOfWork;
        private readonly ILogger _logger;
        private ISysCmsColumnService _SysCmsColumnService;
        private ISysCmsInfoService _SysCmsInfoService;
        public HomeController(UnitOfWork unitOfWork,ILogger<HomeController> logger, ISysCmsColumnService SysCmsColumnService, ISysCmsInfoService SysCmsInfoService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _SysCmsColumnService = SysCmsColumnService;
            _SysCmsInfoService = SysCmsInfoService;
        }
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        /// <summary>
        /// 主页
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Index()
        {
            //轮播图
            var LinkLunBos = await _unitOfWork.SysLinkItemRepository.GetListAsync(o => o.SysLinkTypeId == "4d5c912e76f4462480804bbe91b3365e" && o.Status == 1, "Sort");
            ViewBag.LinkLunBos = LinkLunBos;
            //公告
            var LinkGongGaos = await _unitOfWork.SysLinkItemRepository.GetListAsync(o => o.SysLinkTypeId == "5d41ab158c044ef7a50925b33182ae6e" && o.Status == 1, "Sort");
            ViewBag.LinkGongGaos = LinkGongGaos;

            //点击排行取10条
            var InfoTopCliks = await _unitOfWork.SysCmsInfoRepository.GetInfoPageAsync("", "","2", "1", $"-{nameof(SysCmsInfo.InfoClicks)}", 1, 10);
            ViewBag.InfoTopCliks = InfoTopCliks;

            //评论排行取10条
            var InfoTopComents = await _unitOfWork.SysCmsInfoRepository.GetInfoPageAsync("", "", "2", "1", $"-{nameof(SysCmsInfo.InfoComments)}", 1, 10);
            ViewBag.InfoTopComents = InfoTopComents;

            //站长推荐取链接（暂时不做推荐）
            //var LinkZZTJs = await _unitOfWork.SysLinkItemRepository.GetPageAsync(o => o.SysLinkTypeId == "5d41ab158c044ef7a50925b33182ae6e" && o.Status == 1, "Sort",1,10);
            //ViewBag.LinkZZTJs = LinkZZTJs;

            //站长介绍
            var SetZZJS = await _unitOfWork.SysSettingRepository.GetAsync("SiteIntroduction");
            ViewBag.SetZZJS = SetZZJS ?? new SysSetting();

            //签名
            var SetSignature = await _unitOfWork.SysSettingRepository.GetAsync("SiteSignature");
            ViewBag.Signature = SetSignature?.SetValue;

            //热评用户
            var commentUsers = await _unitOfWork.SysCmsInfoCommentRepository.Load(o => o.Status == 1).Select(o => o.SysUserId).ToListAsync();
            Dictionary<string, int> dicCommentUser = new Dictionary<string, int>();
            foreach (var user in commentUsers)
            {
                if (dicCommentUser.ContainsKey(user))
                {
                    dicCommentUser[user] = dicCommentUser[user] + 1;
                }
                else
                {
                    dicCommentUser.Add(user, 1);
                }
            }
            var users = await _unitOfWork.SysUserRepository.GetListAsync(o => dicCommentUser.Keys.Contains(o.SysUserId), "CreateTime");//获取所有用户资料
            var lstCommentUserCount = dicCommentUser.OrderByDescending(o => o.Value).ToList();
            var sortdicCommentUser = new List<KeyValuePair<string, SysUser>>();
            int count = 0;//设置显示数量从0到4为5条
            foreach (var UserCount in lstCommentUserCount)
            {
                if (count > 4) break;
                sortdicCommentUser.Add(new KeyValuePair<string, SysUser>(UserCount.Key, users.FirstOrDefault(o => o.SysUserId == UserCount.Key)));
                count++;
            }
            ViewBag.sortdicCommentUser = sortdicCommentUser;//存前5个用户资料
            ViewBag.lstCommentUserCount = lstCommentUserCount;//存前5个用户评论数量

            //最近评论(不包含二级评论)10条
            var comments = (await _unitOfWork.SysCmsInfoCommentRepository.GetPageAsync(o => o.Status == 1 && string.IsNullOrEmpty(o.ToUserId), "-CommentTime",1,10,false)).Rows;
            ViewBag.comments = comments;
            var commentUsersRecent = new List<SysUser>();
            foreach (var item in comments)
            {
                var _user = await _unitOfWork.SysUserRepository.GetAsync(item.SysUserId);
                commentUsersRecent.Add(_user ?? new SysUser());
            }
            ViewBag.commentUsersRecent = commentUsersRecent;

            //友情链接10条
            var LinkYQLJs = await _unitOfWork.SysLinkItemRepository.GetPageAsync(o => o.SysLinkTypeId == "035c7d6a65474acfbc909f561fdb98da" && o.Status == 1, "Sort",1,10);
            ViewBag.LinkYQLJs = LinkYQLJs.Rows;

            return View();
        }
        /// <summary>
        /// 获取文章数据
        /// </summary>
        /// <param name="page"></param>
        /// <param name="SysCmsColumnId"></param>
        /// <param name="keywords"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetInfoData(int page = 1,string SysCmsColumnId="",string keywords="")
        {
            int Limit = 10;
            var predicate = PredicateBuilder.New<SysCmsInfo>(true);//查询条件
            predicate = predicate.And(i => i.Status.Equals(1));

            var articleData = await _unitOfWork.SysCmsInfoRepository.GetInfoPageAsync(SysCmsColumnId, keywords, "2", "1", $"-{nameof(SysCmsInfo.InfoTop)},{nameof(SysCmsInfo.Sort)},-{nameof(SysCmsInfo.InfoPublishTime)}",page,Limit);
            foreach (var item in articleData)
            {
                item.InfoType = (await _unitOfWork.SysCmsColumnRepository.GetAsync(item.SysCmsColumnId))?.ColumnName;
                item.InfoImage = string.IsNullOrEmpty(item.InfoImage) ? "" : item.InfoImage.Split(',')[0];
            }
            //总页数
            var totals = await _unitOfWork.SysCmsInfoRepository.CountAsync(predicate);
            long pages = totals % Limit == 0 ? totals / Limit : totals / Limit + 1;
            return SuccessRes(articleData, pages);
        }

        /// <summary>
        /// 关于
        /// </summary>
        /// <returns></returns>
        public async  Task<IActionResult> About()
        {
            var articleData = await _unitOfWork.SysCmsInfoRepository.Load(o => o.Status == 1 && o.SysCmsColumnId == "9359ffe45c8a4f8591194a770aec3dcc").ToListAsync();
            //关于博客
            ViewBag.gybk = articleData.FirstOrDefault(o => o.SysCmsInfoId == "cc002b8d9bc746da9c8a44fe98f132fe") ?? new SysCmsInfo();
            //关于作者
            ViewBag.gyzz = articleData.FirstOrDefault(o => o.SysCmsInfoId == "83981d1cb9c14c94bc49c3890d2970ea") ?? new SysCmsInfo();
            //留言墙
            ViewBag.lyq = articleData.FirstOrDefault(o => o.SysCmsInfoId == "722566ccdb4a4ac892057092e7e9e469") ?? new SysCmsInfo();

            //友情链接
            ViewBag.yqll = articleData.FirstOrDefault(o => o.SysCmsInfoId == "55a464e074414ce9aeff079d568c123e") ?? new SysCmsInfo();
            ViewBag.yqllImg = _unitOfWork.SysLinkTypeRepository.Get("035c7d6a65474acfbc909f561fdb98da")?.SysLinkTypeLogo;
            ViewBag.yqljLinks = await _unitOfWork.SysLinkItemRepository.Load(o => o.SysLinkTypeId == "035c7d6a65474acfbc909f561fdb98da" && o.Status == 1).OrderBy(o => o.Sort).ToListAsync();

            //留言墙
            ViewBag.lyq = articleData.FirstOrDefault(o => o.SysCmsInfoId == "722566ccdb4a4ac892057092e7e9e469") ?? new SysCmsInfo();



            return View();
        }
        /// <summary>
        /// 文章列表
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Article(string id="")
        {
            ViewBag.id = id;
            //点击排行取10条
            var InfoTopCliks = await _unitOfWork.SysCmsInfoRepository.GetInfoPageAsync("", "", "2", "1", $"-{nameof(SysCmsInfo.InfoClicks)}", 1, 10);
            ViewBag.InfoTopCliks = InfoTopCliks;

            //评论排行取10条
            var InfoTopComents = await _unitOfWork.SysCmsInfoRepository.GetInfoPageAsync("", "", "2", "1", $"-{nameof(SysCmsInfo.InfoComments)}", 1, 10);
            ViewBag.InfoTopComents = InfoTopComents;

            ViewBag.SysCmsColumnSelect = _SysCmsColumnService.GetColumnList("87cb34a9bb084d229f137d4f09b2d742");//分类导航

            return View();
        }
        /// <summary>
        /// 详情
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Detail(string id)
        {
            var model = _unitOfWork.SysCmsInfoRepository.Get(id);
            ViewBag.SysCmsColumnSelect = _SysCmsColumnService.GetColumnList("87cb34a9bb084d229f137d4f09b2d742");//分类导航

            //该栏目下的最近10条
            var InfoTopCliks = await _unitOfWork.SysCmsInfoRepository.GetInfoPageAsync(model?.SysCmsColumnId, "", "2", "1", $"-{nameof(SysCmsInfo.InfoClicks)}", 1, 10);
            ViewBag.InfoTopCliks = InfoTopCliks;

            //评论排行取10条
            var InfoTopComents = await _unitOfWork.SysCmsInfoRepository.GetInfoPageAsync("", "", "2", "1", $"-{nameof(SysCmsInfo.InfoComments)}", 1, 10);
            ViewBag.InfoTopComents = InfoTopComents;

            //点击数增加
            if (model != null)
            {
                model.InfoClicks += 1;
                await _unitOfWork.SysCmsInfoRepository.UpdateAsync(model);
            }
            return View(model ?? new SysCmsInfo());
        }
        /// <summary>
        /// 图片（暂时不做）
        /// </summary>
        /// <returns></returns>
        public IActionResult Picture()
        {
            return View();
        }
        /// <summary>
        /// 时间轴
        /// </summary>
        /// <returns></returns>
        public IActionResult Timeline()
        {
            return View();
        }
        /// <summary>
        /// 时间轴数据获取
        /// </summary>
        /// <returns></returns>
        public IActionResult GetTimelineData()
        {
            var data = _SysCmsInfoService.GetTimeLineData();
            return SuccessRes(data);
        }

    }
}
