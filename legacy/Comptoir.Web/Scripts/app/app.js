// Comptoir Durand - interface commerciale (AngularJS 1.x)
(function () {
    'use strict';

    var app = angular.module('comptoir', ['ngRoute']);

    app.config(['$routeProvider', function ($routeProvider) {
        $routeProvider
            .when('/commandes', { templateUrl: 'Scripts/app/views/orders.html', controller: 'OrdersCtrl' })
            .when('/commandes/nouvelle', { templateUrl: 'Scripts/app/views/order-new.html', controller: 'NewOrderCtrl' })
            .when('/commandes/:id', { templateUrl: 'Scripts/app/views/order.html', controller: 'OrderCtrl' })
            .when('/catalogue', { templateUrl: 'Scripts/app/views/catalog.html', controller: 'CatalogCtrl' })
            .when('/clients', { templateUrl: 'Scripts/app/views/customers.html', controller: 'CustomersCtrl' })
            .otherwise({ redirectTo: '/commandes' });
    }]);

    app.controller('OrdersCtrl', ['$scope', '$http', '$location', function ($scope, $http, $location) {
        $scope.status = '';
        $scope.load = function () {
            $http.get('api/orders', { params: { status: $scope.status === '' ? null : $scope.status } })
                .then(function (r) { $scope.orders = r.data; });
        };
        $scope.open = function (o) { $location.path('/commandes/' + o.id); };
        $scope.load();
    }]);

    app.controller('OrderCtrl', ['$scope', '$http', '$routeParams', function ($scope, $http, $routeParams) {
        $scope.load = function () {
            $http.get('api/orders/' + $routeParams.id).then(function (r) { $scope.order = r.data; });
        };
        $scope.act = function (action) {
            $scope.error = null;
            $http.post('api/orders/' + $routeParams.id + '/' + action).then($scope.load, function (r) {
                $scope.error = typeof r.data === 'string' ? r.data : (r.data && r.data.message) || 'Erreur';
            });
        };
        $scope.load();
    }]);

    app.controller('NewOrderCtrl', ['$scope', '$http', '$location', function ($scope, $http, $location) {
        $scope.order = { customerId: null, comment: '', lines: [] };
        $http.get('api/customers').then(function (r) { $scope.customers = r.data; });
        $http.get('api/products').then(function (r) { $scope.products = r.data; });
        $scope.addLine = function () { $scope.order.lines.push({ productId: null, quantity: 1 }); };
        $scope.removeLine = function (i) { $scope.order.lines.splice(i, 1); };
        $scope.save = function () {
            $scope.error = null;
            $http.post('api/orders', $scope.order).then(function (r) {
                $location.path('/commandes/' + r.data.id);
            }, function (r) { $scope.error = r.data && r.data.message ? r.data.message : 'Erreur'; });
        };
        $scope.addLine();
    }]);

    app.controller('CatalogCtrl', ['$scope', '$http', function ($scope, $http) {
        $scope.search = '';
        $scope.load = function () {
            $http.get('api/products', { params: { search: $scope.search } }).then(function (r) { $scope.products = r.data; });
        };
        $scope.load();
    }]);

    app.controller('CustomersCtrl', ['$scope', '$http', function ($scope, $http) {
        $http.get('api/customers').then(function (r) { $scope.customers = r.data; });
    }]);
})();
