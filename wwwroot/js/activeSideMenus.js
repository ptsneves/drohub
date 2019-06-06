/*
* Add on script to activate classes when selects side bar menus.
*
*/

var url = window.location;

// Will only work if string in href matches with location
$('ul.sidebar-menu a[href="' + url + '"]').parent().addClass('active');

// Will also work for relative and absolute hrefs
var menu = $('ul.sidebar-menu a').filter(function () {
    return this.href == url || url.href.indexOf(this.href + "/") == 0;
            }).parent().addClass('active');

var mainParent = menu.parent().parent();

if (mainParent.hasClass("treeview")) {
    mainParent.addClass("active");
}