/** Notice * This file contains works from many authors under various (but compatible) licenses. Please see core.txt for more information. **/
(function(){(window.wpCoreControlsBundle=window.wpCoreControlsBundle||[]).push([[21],{537:function(wa,ta,n){n.r(ta);var pa=n(0),na=n(7),oa=n(3);wa=n(51);var ia=n(29),ka=n(13);n=function(){function ea(){this.init()}ea.prototype.init=function(){this.iha=!1;this.Tf=this.yn=this.connection=null;this.Su={};this.ga=this.bK=null};ea.prototype.zBa=function(z){for(var x=this,f=0;f<z.length;++f){var e=z[f];switch(e.at){case "create":this.Su[e.author]||(this.Su[e.author]=e.aName);this.Bqa(e);break;case "modify":this.ga.sq(e.xfdf).then(function(a){x.ga.sb(a[0])});
break;case "delete":this.ga.sq("<delete><id>"+e.aId+"</id></delete>")}}};ea.prototype.Bqa=function(z){var x=this;this.ga.sq(z.xfdf).then(function(f){f=f[0];f.authorId=z.author;x.ga.sb(f);x.ga.trigger(na.c.UPDATE_ANNOTATION_PERMISSION,[f])})};ea.prototype.Ypa=function(z,x,f){this.yn&&this.yn(z,x,f)};ea.prototype.preloadAnnotations=function(z){this.addEventListener("webViewerServerAnnotationsEnabled",this.Ypa.bind(this,z,"add",{imported:!1}),{once:!0})};ea.prototype.initiateCollaboration=function(z,
x,f){var e=this;if(z){e.Tf=x;e.ga=f.la();f.addEventListener(na.h.DOCUMENT_UNLOADED,function(){e.disableCollaboration()});e.cCa(z);var a=new XMLHttpRequest;a.addEventListener("load",function(){if(200===a.status&&0<a.responseText.length)try{var b=JSON.parse(a.responseText);e.connection=exports.da.QCa(Object(ia.k)(e.Tf,"blackbox/"),"annot");e.bK=b.id;e.Su[b.id]=b.user_name;e.ga.GS(b.id);e.connection.qGa(function(h){h.t&&h.t.startsWith("a_")&&h.data&&e.zBa(h.data)},function(){e.connection.send({t:"a_retrieve",
dId:z});e.trigger(ea.Events.WEBVIEWER_SERVER_ANNOTATIONS_ENABLED,[e.Su[b.id],e.bK])},function(){e.disableCollaboration()})}catch(h){Object(oa.g)(h.message)}});a.open("GET",Object(ia.k)(this.Tf,"demo/SessionInfo.jsp"));a.withCredentials=!0;a.send();e.iha=!0;e.ga.A7(function(b){return e.Su[b.Author]||b.Author})}else Object(oa.g)("Document ID required for collaboration")};ea.prototype.disableCollaboration=function(){this.yn&&(this.ga.removeEventListener(ka.a.Events.ANNOTATION_CHANGED,this.yn),this.yn=
null);this.connection&&this.connection.gs();this.ga&&this.ga.GS("Guest");this.init();this.trigger(ea.Events.WEBVIEWER_SERVER_ANNOTATIONS_DISABLED)};ea.prototype.cCa=function(z){var x=this;this.yn&&this.ga.removeEventListener(ka.a.Events.ANNOTATION_CHANGED,this.yn);this.yn=function(f,e,a){return Object(pa.b)(this,void 0,void 0,function(){var b,h,r,w,y,aa,ha,ca,fa;return Object(pa.d)(this,function(ba){switch(ba.label){case 0:if(a.imported)return[2];b={t:"a_"+e,dId:z,annots:[]};return[4,x.ga.F_()];case 1:h=
ba.ba();"delete"!==e&&(r=(new DOMParser).parseFromString(h,"text/xml"),w=new XMLSerializer);for(y=0;y<f.length;y++)aa=f[y],ca=ha=void 0,"add"===e?(ha=r.querySelector('[name="'+aa.Id+'"]'),ca=w.serializeToString(ha),fa=null,aa.InReplyTo&&(fa=x.ga.kg(aa.InReplyTo).authorId||"default"),b.annots.push({at:"create",aId:aa.Id,author:x.bK,aName:x.Su[x.bK],parent:fa,xfdf:"<add>"+ca+"</add>"})):"modify"===e?(ha=r.querySelector('[name="'+aa.Id+'"]'),ca=w.serializeToString(ha),b.annots.push({at:"modify",aId:aa.Id,
xfdf:"<modify>"+ca+"</modify>"})):"delete"===e&&b.annots.push({at:"delete",aId:aa.Id});0<b.annots.length&&x.connection.send(b);return[2]}})})}.bind(x);this.ga.addEventListener(ka.a.Events.ANNOTATION_CHANGED,this.yn)};ea.Events={WEBVIEWER_SERVER_ANNOTATIONS_ENABLED:"webViewerServerAnnotationsEnabled",WEBVIEWER_SERVER_ANNOTATIONS_DISABLED:"webViewerServerAnnotationsDisabled"};return ea}();Object(wa.a)(n);ta["default"]=n}}]);}).call(this || window)

//# sourceMappingURL=21.chunk.js.map