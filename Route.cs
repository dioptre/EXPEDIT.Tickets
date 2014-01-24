using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Routing;
using Orchard.Mvc.Routes;

namespace EXPEDIT.Tickets
{
    public class Routes : IRouteProvider
    {
        public void GetRoutes(ICollection<RouteDescriptor> routes)
        {
            foreach (var routeDescriptor in GetRoutes())
                routes.Add(routeDescriptor);
        }

        public IEnumerable<RouteDescriptor> GetRoutes()
        {
            return new[] {
                new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Tickets/{controller}/{action}/{id}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"},
                            {"action", "Index"}
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"}
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"}
                        },
                        new MvcRouteHandler())
                },
                 new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Tickets/{action}/{id}/{name}/{contactid}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"}                            
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"},                          
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"}
                        },
                        new MvcRouteHandler())
                },
                 new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Tickets/{action}/{id}/{name}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"}                            
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"},                          
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"}
                        },
                        new MvcRouteHandler())
                },
                 new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Tickets/{action}/{id}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"}                            
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"},                          
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"}
                        },
                        new MvcRouteHandler())
                },
                 new RouteDescriptor {
                    Priority = 5,
                    Route = new Route(
                        "Tickets/{action}",
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"}                            
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"},                          
                        },
                        new RouteValueDictionary {
                            {"area", "EXPEDIT.Tickets"},
                            {"controller", "User"}
                        },
                        new MvcRouteHandler())
                },
                new RouteDescriptor {
                        Priority = 5,
                        Route = new Route(
                            "Tickets/search",
                            new RouteValueDictionary {
                                {"area", "EXPEDIT.Tickets"},
                                {"controller", "User"},
                                {"action", "search"}
                            },
                            null,
                            new RouteValueDictionary {
                                {"area", "EXPEDIT.Tickets"}
                            },
                            new MvcRouteHandler())
                }

            };
        }
    }
}